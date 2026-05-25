using System.ComponentModel;

namespace UE_DDC_Manager.Models;

public class EngineVersion : INotifyPropertyChanged
{
    private bool _isSelected;

    public string DisplayName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string ConfigFilePath { get; set; } = string.Empty;
    public string CurrentDDCPath { get; set; } = string.Empty;
    public bool IsUE5 { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
