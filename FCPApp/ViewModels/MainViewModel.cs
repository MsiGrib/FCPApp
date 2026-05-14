using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCPApp.Services.FileSystem;

namespace FCPApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusText = "Select the root folder to begin working.";
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _currentPage = "Home";

    public HomeViewModel HomeVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel(
        HomeViewModel homeVm,
        SettingsViewModel settingsVm,
        IFileSystemService? fileSystem = null)
    {
        HomeVm = homeVm;
        SettingsVm = settingsVm;

        HomeVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.StatusText))
                StatusText = HomeVm.StatusText;
            if (e.PropertyName == nameof(HomeViewModel.IsProcessing))
                IsProcessing = HomeVm.IsProcessing;
        };
    }

    #region Commands

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentPage = "Home";
        StatusText = "🏠 Home view";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPage = "Settings";
        StatusText = "⚙️ Settings view";
    }

    #endregion

    #region Public Methods

    public void OnWindowClosing()
        => HomeVm.OnViewDeactivated();

    #endregion
}