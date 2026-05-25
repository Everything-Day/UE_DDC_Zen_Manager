using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UE_DDC_Manager.Models;

namespace UE_DDC_Manager.Services;

public static class EngineScanner
{
    private static readonly Regex VersionFolderRegex = new(
        @"^UE[_\-]?(\d+)[._\-](\d+)(?:[._\-](\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<EngineVersion> ScanDirectory(string rootPath)
    {
        var results = new List<EngineVersion>();
        if (!Directory.Exists(rootPath))
            return results;

        var dirs = Directory.GetDirectories(rootPath);
        foreach (var dir in dirs)
        {
            var folderName = Path.GetFileName(dir);

            // Pattern 1: UE_5_4, UE_4_27, UE-5.4, etc.
            var match = VersionFolderRegex.Match(folderName);
            if (match.Success)
            {
                var engine = BuildEngineVersion(dir, match);
                if (engine != null)
                    results.Add(engine);
                continue;
            }

            // Pattern 2: Standard Epic Games launcher layout — "UE_5.4", "UE_5.5"
            if (folderName.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
            {
                var engine = TryBuildFromDirectory(dir, folderName);
                if (engine != null)
                    results.Add(engine);
                continue;
            }

            // Pattern 3: Folder is the root itself (contains Engine subfolder)
            if (Directory.Exists(Path.Combine(dir, "Engine")))
            {
                var engine = TryBuildFromDirectory(dir, folderName);
                if (engine != null)
                    results.Add(engine);
            }
        }

        // Also check if rootPath itself is an engine root
        if (Directory.Exists(Path.Combine(rootPath, "Engine")))
        {
            var engine = TryBuildFromDirectory(rootPath, Path.GetFileName(rootPath));
            if (engine != null && !results.Any(r => r.InstallPath == engine.InstallPath))
                results.Add(engine);
        }

        return results.OrderBy(e => e.DisplayName).ToList();
    }

    private static EngineVersion? BuildEngineVersion(string dir, Match match)
    {
        int major = int.Parse(match.Groups[1].Value);
        int minor = int.Parse(match.Groups[2].Value);
        string version = $"{major}.{minor}";
        if (match.Groups[3].Success)
            version += $".{match.Groups[3].Value}";

        string configPath = FindBaseEngineIni(dir);
        if (string.IsNullOrEmpty(configPath))
            return null;

        return new EngineVersion
        {
            DisplayName = $"Unreal Engine {version}",
            InstallPath = dir,
            ConfigFilePath = configPath,
            CurrentDDCPath = ReadCurrentDDCPath(configPath),
            IsUE5 = major >= 5
        };
    }

    private static EngineVersion? TryBuildFromDirectory(string dir, string folderName)
    {
        string configPath = FindBaseEngineIni(dir);
        if (string.IsNullOrEmpty(configPath))
            return null;

        string version = ExtractVersionFromBuildFile(dir) ?? folderName;

        return new EngineVersion
        {
            DisplayName = version.StartsWith("Unreal", StringComparison.OrdinalIgnoreCase)
                ? version
                : $"Unreal Engine ({version})",
            InstallPath = dir,
            ConfigFilePath = configPath,
            CurrentDDCPath = ReadCurrentDDCPath(configPath),
            IsUE5 = version.Contains('5')
        };
    }

    private static string FindBaseEngineIni(string engineRoot)
    {
        string path = Path.Combine(engineRoot, "Engine", "Config", "BaseEngine.ini");
        return File.Exists(path) ? path : string.Empty;
    }

    private static string? ExtractVersionFromBuildFile(string engineRoot)
    {
        string buildVersionPath = Path.Combine(engineRoot, "Engine", "Build", "Build.version");
        if (!File.Exists(buildVersionPath))
            return null;

        try
        {
            string json = File.ReadAllText(buildVersionPath);
            var majorMatch = Regex.Match(json, @"""MajorVersion""\s*:\s*(\d+)");
            var minorMatch = Regex.Match(json, @"""MinorVersion""\s*:\s*(\d+)");
            var patchMatch = Regex.Match(json, @"""PatchVersion""\s*:\s*(\d+)");

            if (majorMatch.Success && minorMatch.Success)
            {
                string ver = $"Unreal Engine {majorMatch.Groups[1].Value}.{minorMatch.Groups[1].Value}";
                if (patchMatch.Success)
                    ver += $".{patchMatch.Groups[1].Value}";
                return ver;
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return null;
    }

    public static string ReadCurrentDDCPath(string configFilePath)
    {
        if (!File.Exists(configFilePath))
            return "(未找到配置文件)";

        try
        {
            var lines = File.ReadAllLines(configFilePath, Encoding.UTF8);
            bool inDDCSection = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith('['))
                {
                    inDDCSection = trimmed.Equals("[InstalledDerivedDataBackendGraph]",
                        StringComparison.OrdinalIgnoreCase)
                        || trimmed.Equals("[DerivedDataBackendGraph]",
                            StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inDDCSection) continue;

                // Look for path definitions in the DDC graph
                var pathMatch = Regex.Match(trimmed, @"Path\s*=\s*(.+)", RegexOptions.IgnoreCase);
                if (pathMatch.Success)
                {
                    string path = pathMatch.Groups[1].Value.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
        }
        catch
        {
            return "(读取失败)";
        }

        return "(使用默认路径)";
    }
}
