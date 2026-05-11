using CommunityToolkit.Mvvm.ComponentModel;

namespace FCPApp.Models;

public partial class FolderItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _fullPath = string.Empty;
    [ObservableProperty] private bool _isSelected = false;
}