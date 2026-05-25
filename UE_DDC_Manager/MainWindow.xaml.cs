using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using UE_DDC_Manager.Models;
using UE_DDC_Manager.Services;
using MessageBox = System.Windows.MessageBox;

namespace UE_DDC_Manager;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<EngineVersion> _engines = new();
    private bool _isChinese = true;

    public MainWindow()
    {
        InitializeComponent();
        LvEngines.ItemsSource = _engines;
        ChkSharedDDC.Checked += (_, _) => PnlSharedDDC.IsEnabled = true;
        ChkSharedDDC.Unchecked += (_, _) => PnlSharedDDC.IsEnabled = false;
        RefreshEnvVarStatus();
        RefreshCacheInfo();
    }

    private void BtnLangToggle_Click(object sender, RoutedEventArgs e)
    {
        _isChinese = !_isChinese;
        ApplyLanguage();
        RefreshEnvVarStatus();
        RefreshCacheInfo();
    }

    private void ApplyLanguage()
    {
        if (_isChinese)
        {
            Title = "虚幻引擎 DDC、Zen 缓存管理工具";
            TxtHeaderTitle.Text = "虚幻引擎 DDC、Zen 缓存管理工具";
            TxtHeaderSubtitle.Text = "一键修改 Derived Data Cache 存储位置，释放系统盘空间";
            GrpModule1.Header = "模块一：引擎版本检测";
            TxtEnginePathLabel.Text = "指定虚幻引擎安装根目录：";
            BtnBrowseEngine.Content = "浏览...";
            BtnScan.Content = "扫描引擎";
            GrpEngineList.Header = "检测到的引擎版本（勾选需要修改的版本）";
            TxtNoEngines.Text = "未检测到引擎版本。请指定路径后点击【扫描引擎】。";
            GrpModule2.Header = "模块二：指定新缓存位置";
            TxtCachePathLabel.Text = "选择新的 DDC 存储目录（同时控制 Local DDC 和 Zen Server）：";
            BtnBrowseCache.Content = "浏览文件夹...";
            GrpSharedDDC.Header = "高级选项：Shared DDC（团队共享缓存）";
            ChkSharedDDC.Content = "同时设置 Shared DDC 路径（UE-SharedDataCachePath）";
            BtnBrowseShared.Content = "浏览文件夹...";
            TxtSharedDDCDesc.Text = "Shared DDC 用于团队内共享编译后的派生数据，可加速团队成员首次加载。个人开发者通常无需设置。";
            GrpMethod.Header = "模块三：修改方式";
            RbEnvVar.Content = "方式一：设置用户环境变量（推荐 — 全局生效，无需修改配置文件）";
            RbConfigFile.Content = "方式二：修改 BaseEngine.ini 配置文件（仅对选中的引擎版本生效）";
            RbBoth.Content = "方式三：两种方式同时应用（双重保险）";
            TxtEnvStatusTitle.Text = "当前环境变量状态";
            TxtEnvLocalLabel.Text = "UE-LocalDataCachePath：";
            TxtEnvSharedLabel.Text = "UE-SharedDataCachePath：";
            BtnRemoveEnvVar.Content = "移除缓存环境变量";
            BtnRemoveSharedVar.Content = "移除 Shared DDC 变量";
            TxtCleanTitle.Text = "清理默认缓存";
            BtnCleanCache.Content = "清理旧缓存";
            BtnSelectAll.Content = "全选/取消全选";
            BtnExecute.Content = "一键修改缓存位置";
        }
        else
        {
            Title = "UE DDC & Zen Cache Manager";
            TxtHeaderTitle.Text = "UE DDC & Zen Cache Manager";
            TxtHeaderSubtitle.Text = "Redirect Derived Data Cache storage to free up system drive space";
            GrpModule1.Header = "Module 1: Engine Detection";
            TxtEnginePathLabel.Text = "Specify Unreal Engine installation root:";
            BtnBrowseEngine.Content = "Browse...";
            BtnScan.Content = "Scan";
            GrpEngineList.Header = "Detected engine versions (check versions to modify)";
            TxtNoEngines.Text = "No engine versions detected. Specify path and click [Scan].";
            GrpModule2.Header = "Module 2: New Cache Location";
            TxtCachePathLabel.Text = "Select new DDC storage directory (controls both Local DDC and Zen Server):";
            BtnBrowseCache.Content = "Browse folder...";
            GrpSharedDDC.Header = "Advanced: Shared DDC (Team Shared Cache)";
            ChkSharedDDC.Content = "Also set Shared DDC path (UE-SharedDataCachePath)";
            BtnBrowseShared.Content = "Browse folder...";
            TxtSharedDDCDesc.Text = "Shared DDC shares compiled derived data across team members, speeding up first load. Not needed for solo developers.";
            GrpMethod.Header = "Module 3: Modification Method";
            RbEnvVar.Content = "Method 1: Set user environment variable (Recommended - global, no config file changes)";
            RbConfigFile.Content = "Method 2: Modify BaseEngine.ini (only affects selected engine versions)";
            RbBoth.Content = "Method 3: Apply both methods (double insurance)";
            TxtEnvStatusTitle.Text = "Current Environment Variable Status";
            TxtEnvLocalLabel.Text = "UE-LocalDataCachePath:";
            TxtEnvSharedLabel.Text = "UE-SharedDataCachePath:";
            BtnRemoveEnvVar.Content = "Remove Cache Env Var";
            BtnRemoveSharedVar.Content = "Remove Shared DDC Var";
            TxtCleanTitle.Text = "Clean Default Cache";
            BtnCleanCache.Content = "Clean Cache";
            BtnSelectAll.Content = "Select All / Deselect";
            BtnExecute.Content = "Apply Cache Redirect";
        }
    }

    private void BtnBrowseEngine_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _isChinese ? "选择虚幻引擎安装根目录" : "Select Unreal Engine installation root",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrEmpty(TxtEnginePath.Text) && Directory.Exists(TxtEnginePath.Text))
            dialog.InitialDirectory = TxtEnginePath.Text;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtEnginePath.Text = dialog.SelectedPath;
            PerformScan();
        }
    }

    private void BtnBrowseCache_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _isChinese ? "选择新的 DDC 缓存存储目录" : "Select new DDC cache storage directory",
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtNewCachePath.Text = dialog.SelectedPath;
    }

    private void BtnBrowseShared_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _isChinese ? "选择 Shared DDC 存储目录" : "Select Shared DDC storage directory",
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtSharedDDCPath.Text = dialog.SelectedPath;
    }

    private void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        PerformScan();
    }

    private void PerformScan()
    {
        string path = TxtEnginePath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ShowWarning(_isChinese ? "请先输入引擎安装根目录路径。" : "Please enter the engine installation root path first.");
            return;
        }

        if (!Directory.Exists(path))
        {
            ShowWarning(_isChinese ? $"路径不存在：{path}" : $"Path does not exist: {path}");
            return;
        }

        _engines.Clear();
        var found = EngineScanner.ScanDirectory(path);

        foreach (var engine in found)
        {
            engine.IsSelected = true;
            _engines.Add(engine);
        }

        TxtNoEngines.Visibility = _engines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_engines.Count == 0)
            ShowInfo(_isChinese
                ? "未在该目录下检测到虚幻引擎。\n请确认目录中包含 Engine/Config/BaseEngine.ini 的引擎文件夹。"
                : "No Unreal Engine found in this directory.\nPlease ensure the folder contains Engine/Config/BaseEngine.ini.");
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool anySelected = _engines.Any(eng => eng.IsSelected);
        bool newState = !anySelected;
        foreach (var engine in _engines)
            engine.IsSelected = newState;
    }

    private void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        string newPath = TxtNewCachePath.Text.Trim();
        if (string.IsNullOrEmpty(newPath))
        {
            ShowWarning(_isChinese ? "请先指定新的缓存存储路径。" : "Please specify the new cache storage path first.");
            return;
        }

        if (!IsValidPath(newPath))
        {
            ShowWarning(_isChinese ? "指定的路径格式无效，请检查。" : "Invalid path format. Please check.");
            return;
        }

        if (ChkSharedDDC.IsChecked == true)
        {
            string sharedPath = TxtSharedDDCPath.Text.Trim();
            if (string.IsNullOrEmpty(sharedPath))
            {
                ShowWarning(_isChinese ? "已启用 Shared DDC 选项，请指定 Shared DDC 路径。" : "Shared DDC is enabled. Please specify the Shared DDC path.");
                return;
            }
            if (!IsValidPath(sharedPath))
            {
                ShowWarning(_isChinese ? "Shared DDC 路径格式无效，请检查。" : "Shared DDC path format is invalid. Please check.");
                return;
            }
        }

        var selectedEngines = _engines.Where(eng => eng.IsSelected).ToList();

        DDCModifyMethod method;
        if (RbEnvVar.IsChecked == true)
            method = DDCModifyMethod.EnvironmentVariable;
        else if (RbConfigFile.IsChecked == true)
            method = DDCModifyMethod.ConfigFile;
        else
            method = DDCModifyMethod.Both;

        if (method == DDCModifyMethod.ConfigFile || method == DDCModifyMethod.Both)
        {
            if (selectedEngines.Count == 0)
            {
                ShowWarning(_isChinese
                    ? "请至少选择一个引擎版本（勾选列表中的引擎）。"
                    : "Please select at least one engine version (check engines in the list).");
                return;
            }
        }

        string confirmMsg = BuildConfirmMessage(method, newPath, selectedEngines,
            ChkSharedDDC.IsChecked == true ? TxtSharedDDCPath.Text.Trim() : null);
        var result = MessageBox.Show(confirmMsg,
            _isChinese ? "确认修改" : "Confirm Changes",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        ExecuteModification(method, newPath, selectedEngines,
            ChkSharedDDC.IsChecked == true ? TxtSharedDDCPath.Text.Trim() : null);
    }

    private void ExecuteModification(DDCModifyMethod method, string newPath,
        List<EngineVersion> engines, string? sharedPath)
    {
        var log = new StringBuilder();
        bool allSuccess = true;

        if (method == DDCModifyMethod.EnvironmentVariable || method == DDCModifyMethod.Both)
        {
            var envResult = DDCModifier.SetEnvironmentVariable(newPath);
            log.AppendLine(_isChinese ? "【环境变量】" : "[Environment Variable]");
            log.AppendLine(envResult.Message);
            log.AppendLine();
            if (!envResult.Success) allSuccess = false;
        }

        if (method == DDCModifyMethod.ConfigFile || method == DDCModifyMethod.Both)
        {
            log.AppendLine(_isChinese ? "【配置文件修改】" : "[Config File Modification]");
            foreach (var engine in engines)
            {
                var configResult = DDCModifier.ModifyConfigFile(engine, newPath);
                log.AppendLine(configResult.Message);
                log.AppendLine();
                if (!configResult.Success) allSuccess = false;
            }
        }

        if (!string.IsNullOrEmpty(sharedPath))
        {
            var sharedResult = DDCModifier.SetSharedEnvironmentVariable(sharedPath);
            log.AppendLine("【Shared DDC】");
            log.AppendLine(sharedResult.Message);
            log.AppendLine();
            if (!sharedResult.Success) allSuccess = false;
        }

        if (allSuccess)
        {
            log.AppendLine("═══════════════════════════════");
            if (_isChinese)
            {
                log.AppendLine("所有修改已成功完成！");
                log.AppendLine();
                log.AppendLine("⚠️ 重要提示：修改完成后，请务必在系统托盘彻底退出");
                log.AppendLine("Epic Games Launcher 及所有后台的虚幻引擎进程");
                log.AppendLine("（包括 ZenServer.exe），再次启动引擎才能完全生效。");
            }
            else
            {
                log.AppendLine("All changes completed successfully!");
                log.AppendLine();
                log.AppendLine("⚠️ Important: After modification, please fully exit");
                log.AppendLine("Epic Games Launcher and all background UE processes");
                log.AppendLine("(including ZenServer.exe) before restarting the engine.");
            }
        }

        string title = allSuccess
            ? (_isChinese ? "修改成功" : "Success")
            : (_isChinese ? "部分操作失败" : "Partial Failure");
        var icon = allSuccess ? MessageBoxImage.Information : MessageBoxImage.Warning;
        MessageBox.Show(log.ToString(), title, MessageBoxButton.OK, icon);

        RefreshEnvVarStatus();
        PerformScan();
    }

    private void BtnRemoveEnvVar_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            _isChinese
                ? "确定要移除环境变量 UE-LocalDataCachePath 吗？\n移除后引擎将使用默认缓存位置。"
                : "Remove environment variable UE-LocalDataCachePath?\nThe engine will use the default cache location after removal.",
            _isChinese ? "确认移除" : "Confirm Removal",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var removeResult = DDCModifier.RemoveEnvironmentVariable();
        MessageBox.Show(removeResult.Message,
            removeResult.Success ? (_isChinese ? "成功" : "Success") : (_isChinese ? "失败" : "Failed"),
            MessageBoxButton.OK, removeResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        RefreshEnvVarStatus();
    }

    private void BtnRemoveSharedVar_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            _isChinese
                ? "确定要移除环境变量 UE-SharedDataCachePath 吗？"
                : "Remove environment variable UE-SharedDataCachePath?",
            _isChinese ? "确认移除" : "Confirm Removal",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var removeResult = DDCModifier.RemoveSharedEnvironmentVariable();
        MessageBox.Show(removeResult.Message,
            removeResult.Success ? (_isChinese ? "成功" : "Success") : (_isChinese ? "失败" : "Failed"),
            MessageBoxButton.OK, removeResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        RefreshEnvVarStatus();
    }

    private void BtnCleanCache_Click(object sender, RoutedEventArgs e)
    {
        var blocking = DDCModifier.GetBlockingProcesses();
        if (blocking.Count > 0)
        {
            ShowWarning(_isChinese
                ? "检测到以下进程正在运行，无法安全清理缓存：\n\n" +
                  string.Join("\n", blocking.Select(p => $"  - {p}")) +
                  "\n\n请先关闭这些进程后再试。\n" +
                  "提示：在系统托盘中彻底退出 Epic Games Launcher，\n" +
                  "并确保 UnrealEditor 和 ZenServer 已完全退出。"
                : "The following processes are running, cannot safely clean cache:\n\n" +
                  string.Join("\n", blocking.Select(p => $"  - {p}")) +
                  "\n\nPlease close these processes first.\n" +
                  "Tip: Fully exit Epic Games Launcher from the system tray,\n" +
                  "and ensure UnrealEditor and ZenServer have fully exited.");
            return;
        }

        var localInfo = DDCModifier.GetDefaultLocalDDCInfo();
        var zenInfo = DDCModifier.GetDefaultZenDataInfo();

        if (localInfo == null && zenInfo == null)
        {
            ShowInfo(_isChinese ? "默认缓存目录不存在，无需清理。" : "Default cache directories do not exist. No cleanup needed.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(_isChinese ? "即将清理以下默认缓存目录的内容：\n" : "About to clean the following default cache directories:\n");

        if (localInfo != null)
            sb.AppendLine($"  Local DDC：{localInfo.Value.Path}\n  {(_isChinese ? "大小" : "Size")}：{FormatSize(localInfo.Value.Size)}\n");
        if (zenInfo != null)
            sb.AppendLine($"  Zen Server：{zenInfo.Value.Path}\n  {(_isChinese ? "大小" : "Size")}：{FormatSize(zenInfo.Value.Size)}\n");

        sb.AppendLine(_isChinese
            ? "此操作是安全的 — 引擎下次启动会自动重新生成缓存。"
            : "This is safe — the engine will regenerate the cache on next startup.");
        sb.AppendLine(_isChinese
            ? "首次重新生成时加载会稍慢。"
            : "First regeneration will be slightly slower.");
        sb.AppendLine(_isChinese ? "\n确认清理？" : "\nConfirm cleanup?");

        var result = MessageBox.Show(sb.ToString(),
            _isChinese ? "确认清理缓存" : "Confirm Cache Cleanup",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var cleanResult = DDCModifier.CleanDefaultCache();
        MessageBox.Show(cleanResult.Message,
            cleanResult.Success ? (_isChinese ? "清理完成" : "Cleanup Complete") : (_isChinese ? "部分清理失败" : "Partial Cleanup Failed"),
            MessageBoxButton.OK, cleanResult.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);

        RefreshCacheInfo();
    }

    private void RefreshEnvVarStatus()
    {
        string? localValue = DDCModifier.GetCurrentEnvVariable();
        string? sharedValue = DDCModifier.GetCurrentSharedEnvVariable();

        var grayBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x99, 0x99, 0x99));
        var blueBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xd4));

        string notSet = _isChinese ? "未设置" : "Not Set";

        TxtCurrentEnvVar.Text = string.IsNullOrEmpty(localValue) ? notSet : localValue;
        TxtCurrentEnvVar.Foreground = string.IsNullOrEmpty(localValue) ? grayBrush : blueBrush;

        TxtCurrentSharedVar.Text = string.IsNullOrEmpty(sharedValue) ? notSet : sharedValue;
        TxtCurrentSharedVar.Foreground = string.IsNullOrEmpty(sharedValue) ? grayBrush : blueBrush;

        BtnRemoveEnvVar.Visibility = !string.IsNullOrEmpty(localValue)
            ? Visibility.Visible : Visibility.Collapsed;
        BtnRemoveSharedVar.Visibility = !string.IsNullOrEmpty(sharedValue)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshCacheInfo()
    {
        var localInfo = DDCModifier.GetDefaultLocalDDCInfo();
        var zenInfo = DDCModifier.GetDefaultZenDataInfo();

        if (localInfo == null && zenInfo == null)
        {
            TxtCacheInfo.Text = _isChinese
                ? "默认缓存目录不存在或已清理。"
                : "Default cache directories do not exist or already cleaned.";
            BtnCleanCache.IsEnabled = false;
        }
        else
        {
            var parts = new List<string>();
            if (localInfo != null)
                parts.Add($"Local DDC: {FormatSize(localInfo.Value.Size)}");
            if (zenInfo != null)
                parts.Add($"Zen Server: {FormatSize(zenInfo.Value.Size)}");

            TxtCacheInfo.Text = _isChinese
                ? $"默认缓存占用：{string.Join("，", parts)}。删除后引擎会自动重新生成。"
                : $"Default cache usage: {string.Join(", ", parts)}. Engine will auto-regenerate after deletion.";
            BtnCleanCache.IsEnabled = true;
        }
    }

    private string BuildConfirmMessage(DDCModifyMethod method, string newPath,
        List<EngineVersion> engines, string? sharedPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_isChinese ? "即将执行以下修改：\n" : "About to apply the following changes:\n");
        sb.AppendLine($"{(_isChinese ? "缓存路径" : "Cache path")}：{newPath}");
        sb.AppendLine(_isChinese
            ? "  （同时控制 Local DDC 和 Zen Server 数据）\n"
            : "  (Controls both Local DDC and Zen Server data)\n");

        switch (method)
        {
            case DDCModifyMethod.EnvironmentVariable:
                sb.AppendLine(_isChinese ? "修改方式：设置用户环境变量" : "Method: Set user environment variable");
                sb.AppendLine("  UE-LocalDataCachePath → " + (_isChinese ? "指定路径" : "specified path"));
                break;
            case DDCModifyMethod.ConfigFile:
                sb.AppendLine(_isChinese ? "修改方式：修改 BaseEngine.ini 配置文件" : "Method: Modify BaseEngine.ini config file");
                sb.AppendLine($"{(_isChinese ? "影响引擎数量" : "Engines affected")}：{engines.Count}");
                foreach (var eng in engines)
                    sb.AppendLine($"  - {eng.DisplayName}");
                break;
            case DDCModifyMethod.Both:
                sb.AppendLine(_isChinese ? "修改方式：环境变量 + 配置文件（双重保险）" : "Method: Environment variable + Config file (double insurance)");
                sb.AppendLine($"{(_isChinese ? "影响引擎数量" : "Engines affected")}：{engines.Count}");
                break;
        }

        if (!string.IsNullOrEmpty(sharedPath))
        {
            sb.AppendLine($"\nShared DDC：{sharedPath}");
            sb.AppendLine("  UE-SharedDataCachePath → " + (_isChinese ? "指定路径" : "specified path"));
        }

        sb.AppendLine(_isChinese ? "\n确认执行？" : "\nConfirm?");
        return sb.ToString();
    }

    private static bool IsValidPath(string path)
    {
        try
        {
            _ = Path.GetFullPath(path);
            return Path.IsPathRooted(path);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private void ShowWarning(string msg)
        => MessageBox.Show(msg, _isChinese ? "提示" : "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ShowInfo(string msg)
        => MessageBox.Show(msg, _isChinese ? "提示" : "Info", MessageBoxButton.OK, MessageBoxImage.Information);
}
