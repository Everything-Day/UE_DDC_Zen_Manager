using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UE_DDC_Manager.Models;

namespace UE_DDC_Manager.Services;

public enum DDCModifyMethod
{
    EnvironmentVariable,
    ConfigFile,
    Both
}

public record DDCModifyResult(bool Success, string Message);

public static class DDCModifier
{
    public static DDCModifyResult SetEnvironmentVariable(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            Environment.SetEnvironmentVariable(
                "UE-LocalDataCachePath", path, EnvironmentVariableTarget.User);

            CleanupLegacyZenDataPath();
            BroadcastSettingChange();

            return new DDCModifyResult(true,
                $"已成功设置用户环境变量：\n" +
                $"  UE-LocalDataCachePath = {path}\n\n" +
                "此变量同时控制 Local DDC 和 Zen Server 数据路径（通过引擎内置 LocalDataCachePathEnvOverride 机制）。\n" +
                "对所有 UE4/UE5 版本全局生效。");
        }
        catch (UnauthorizedAccessException)
        {
            return new DDCModifyResult(false, "权限不足：无法设置环境变量。请以管理员身份运行本程序。");
        }
        catch (Exception ex)
        {
            return new DDCModifyResult(false, $"设置环境变量失败：{ex.Message}");
        }
    }

    public static DDCModifyResult SetSharedEnvironmentVariable(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            Environment.SetEnvironmentVariable(
                "UE-SharedDataCachePath", path, EnvironmentVariableTarget.User);

            BroadcastSettingChange();

            return new DDCModifyResult(true,
                $"已成功设置用户环境变量：\n" +
                $"  UE-SharedDataCachePath = {path}\n\n" +
                "Shared DDC 用于团队共享的派生数据缓存。");
        }
        catch (UnauthorizedAccessException)
        {
            return new DDCModifyResult(false, "权限不足：无法设置环境变量。请以管理员身份运行本程序。");
        }
        catch (Exception ex)
        {
            return new DDCModifyResult(false, $"设置 Shared DDC 环境变量失败：{ex.Message}");
        }
    }

    public static DDCModifyResult ModifyConfigFile(EngineVersion engine, string newPath)
    {
        string configPath = engine.ConfigFilePath;

        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            return new DDCModifyResult(false, $"配置文件不存在：{configPath}");

        try
        {
            if (!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);

            string backupPath = configPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(configPath, backupPath, overwrite: true);

            string content = File.ReadAllText(configPath, Encoding.UTF8);
            string newContent = PatchLocalPathInSection(content, newPath, engine.IsUE5);

            File.WriteAllText(configPath, newContent, Encoding.UTF8);

            return new DDCModifyResult(true,
                $"[{engine.DisplayName}]\n" +
                $"  配置文件已修改：{configPath}\n" +
                $"  备份文件：{backupPath}\n" +
                $"  新缓存路径：{newPath}");
        }
        catch (UnauthorizedAccessException)
        {
            return new DDCModifyResult(false,
                $"权限不足：无法写入 {configPath}\n请以管理员身份运行，或检查文件是否为只读。");
        }
        catch (IOException ex)
        {
            return new DDCModifyResult(false, $"文件操作失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new DDCModifyResult(false, $"修改配置失败：{ex.Message}");
        }
    }

    private static readonly Regex LocalPathRegex = new(
        @"(?<=\bPath\s*=\s*)(?:""[^""]*""|[^,)\s]+)",
        RegexOptions.Compiled);

    private static string PatchLocalPathInSection(string iniContent, string newPath, bool isUE5)
    {
        string escapedPath = newPath.Replace("\\", "/");

        string[] targetSections = isUE5
            ? ["[InstalledDerivedDataBackendGraph]", "[DerivedDataBackendGraph]"]
            : ["[DerivedDataBackendGraph]", "[InstalledDerivedDataBackendGraph]"];

        var sb = new StringBuilder();
        var lines = iniContent.Split('\n');
        bool inTargetSection = false;
        bool patched = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.StartsWith('['))
            {
                inTargetSection = targetSections.Any(s =>
                    trimmed.Equals(s, StringComparison.OrdinalIgnoreCase));
            }

            if (inTargetSection && trimmed.StartsWith("Local=", StringComparison.OrdinalIgnoreCase))
            {
                string patchedLine = LocalPathRegex.Replace(lines[i],
                    $"\"{escapedPath}\"", count: 1);
                sb.Append(patchedLine);
                if (!lines[i].EndsWith('\n'))
                    sb.Append('\n');
                patched = true;
            }
            else
            {
                sb.Append(lines[i]);
                if (i < lines.Length - 1 && !lines[i].EndsWith('\n'))
                    sb.Append('\n');
            }
        }

        if (!patched)
            return iniContent;

        return sb.ToString();
    }

    public static DDCModifyResult RemoveEnvironmentVariable()
    {
        try
        {
            Environment.SetEnvironmentVariable(
                "UE-LocalDataCachePath", null, EnvironmentVariableTarget.User);
            CleanupLegacyZenDataPath();
            BroadcastSettingChange();
            return new DDCModifyResult(true,
                "已移除用户环境变量 UE-LocalDataCachePath");
        }
        catch (Exception ex)
        {
            return new DDCModifyResult(false, $"移除环境变量失败：{ex.Message}");
        }
    }

    public static DDCModifyResult RemoveSharedEnvironmentVariable()
    {
        try
        {
            Environment.SetEnvironmentVariable(
                "UE-SharedDataCachePath", null, EnvironmentVariableTarget.User);
            BroadcastSettingChange();
            return new DDCModifyResult(true,
                "已移除用户环境变量 UE-SharedDataCachePath");
        }
        catch (Exception ex)
        {
            return new DDCModifyResult(false, $"移除 Shared DDC 环境变量失败：{ex.Message}");
        }
    }

    public static List<string> GetBlockingProcesses()
    {
        string[] processNames = ["UnrealEditor", "ZenServer", "EpicGamesLauncher"];
        var running = new List<string>();
        foreach (var name in processNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                    running.Add(name);
            }
            catch { }
        }
        return running;
    }

    public static DDCModifyResult CleanDefaultCache()
    {
        var log = new StringBuilder();
        bool allSuccess = true;
        int skippedCount = 0;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string defaultLocalDDC = Path.Combine(localAppData, "UnrealEngine", "Common", "DerivedDataCache");
        string defaultZenData = Path.Combine(localAppData, "UnrealEngine", "Common", "Zen", "Data");

        long totalFreed = 0;

        if (Directory.Exists(defaultLocalDDC))
        {
            var (freed, skipped) = CleanDirectoryContents(defaultLocalDDC);
            totalFreed += freed;
            skippedCount += skipped;
            if (skipped > 0)
            {
                allSuccess = false;
                log.AppendLine($"△ Local DDC 缓存已部分清理：{defaultLocalDDC}");
                log.AppendLine($"  释放空间：{FormatSize(freed)}，跳过 {skipped} 个无法删除的项目");
            }
            else
            {
                log.AppendLine($"✓ Local DDC 缓存已清理：{defaultLocalDDC}");
                log.AppendLine($"  释放空间：{FormatSize(freed)}");
            }
        }
        else
        {
            log.AppendLine($"- Local DDC 默认缓存不存在（已清理或未生成）");
            log.AppendLine($"  路径：{defaultLocalDDC}");
        }

        log.AppendLine();

        if (Directory.Exists(defaultZenData))
        {
            var (freed, skipped) = CleanDirectoryContents(defaultZenData);
            totalFreed += freed;
            skippedCount += skipped;
            if (skipped > 0)
            {
                allSuccess = false;
                log.AppendLine($"△ Zen Server 缓存已部分清理：{defaultZenData}");
                log.AppendLine($"  释放空间：{FormatSize(freed)}，跳过 {skipped} 个无法删除的项目");
            }
            else
            {
                log.AppendLine($"✓ Zen Server 缓存已清理：{defaultZenData}");
                log.AppendLine($"  释放空间：{FormatSize(freed)}");
            }
        }
        else
        {
            log.AppendLine($"- Zen Server 默认缓存不存在（已清理或未生成）");
            log.AppendLine($"  路径：{defaultZenData}");
        }

        if (totalFreed > 0)
        {
            log.AppendLine();
            log.AppendLine($"共释放磁盘空间：{FormatSize(totalFreed)}");
        }

        if (skippedCount > 0)
        {
            log.AppendLine();
            log.AppendLine($"有 {skippedCount} 个文件/文件夹因权限或占用问题无法删除，已跳过。");
            log.AppendLine("这不会影响引擎正常使用，可稍后重试或手动删除。");
        }

        log.AppendLine();
        log.AppendLine("说明：删除缓存是安全的，引擎下次启动时会自动重新生成所需的缓存数据。");
        log.AppendLine("首次重新生成时加载速度会稍慢，之后恢复正常。");

        return new DDCModifyResult(allSuccess, log.ToString());
    }

    private static (long freedBytes, int skippedCount) CleanDirectoryContents(string directoryPath)
    {
        long freed = 0;
        int skipped = 0;

        // Delete files first
        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    long size = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += size;
                }
                catch
                {
                    skipped++;
                }
            }
        }
        catch { }

        // Then delete subdirectories (bottom-up by using depth-first ordering)
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch
                {
                    skipped++;
                }
            }
        }
        catch { }

        return (freed, skipped);
    }

    public static string? GetCurrentEnvVariable()
    {
        return Environment.GetEnvironmentVariable(
            "UE-LocalDataCachePath", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(
                "UE-LocalDataCachePath", EnvironmentVariableTarget.Machine);
    }

    public static string? GetCurrentSharedEnvVariable()
    {
        return Environment.GetEnvironmentVariable(
            "UE-SharedDataCachePath", EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(
                "UE-SharedDataCachePath", EnvironmentVariableTarget.Machine);
    }

    public static (string Path, long Size)? GetDefaultLocalDDCInfo()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string path = Path.Combine(localAppData, "UnrealEngine", "Common", "DerivedDataCache");
        if (!Directory.Exists(path)) return null;
        return (path, GetDirectorySize(path));
    }

    public static (string Path, long Size)? GetDefaultZenDataInfo()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string path = Path.Combine(localAppData, "UnrealEngine", "Common", "Zen", "Data");
        if (!Directory.Exists(path)) return null;
        return (path, GetDirectorySize(path));
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public static string? GetLegacyZenDataPath()
    {
        return Environment.GetEnvironmentVariable(
            "UE-ZenDataPath", EnvironmentVariableTarget.User);
    }

    private static void CleanupLegacyZenDataPath()
    {
        if (!string.IsNullOrEmpty(GetLegacyZenDataPath()))
        {
            Environment.SetEnvironmentVariable(
                "UE-ZenDataPath", null, EnvironmentVariableTarget.User);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private static void BroadcastSettingChange()
    {
        const uint WM_SETTINGCHANGE = 0x001A;
        const uint SMTO_ABORTIFHUNG = 0x0002;
        IntPtr HWND_BROADCAST = new(0xffff);
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero,
            "Environment", SMTO_ABORTIFHUNG, 5000, out _);
    }
}
