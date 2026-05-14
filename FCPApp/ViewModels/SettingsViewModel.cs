using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FCPApp.Models;
using FCPApp.Services.Config;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FCPApp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _settingsStatus = "Settings loaded";

    private AppSettings _currentSettings = null!;

    public ICommand OnStartWithWindowsChangedCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }

    public SettingsViewModel()
    {
        Console.WriteLine($"[SETTINGS] ⚙️ Constructor called (HashCode: {GetHashCode()})");

        OnStartWithWindowsChangedCommand = new RelayCommand(OnStartWithWindowsChanged);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);

        LoadSettings();
    }

    #region Public Methods

    public static string GetSettingsPathForDebug()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FCPApp", "appsettings.json");

    private void SaveSettings()
    {
        _currentSettings = _currentSettings with
        {
            StartWithWindows = StartWithWindows
        };

        AppSettingsService.Save(_currentSettings);
        AppSettingsService.ApplyStartWithWindows(StartWithWindows);

        SettingsStatus = $"Saved ✓ {DateTime.Now:HH:mm:ss}";

        _ = Task.Delay(2000).ContinueWith(_ =>
            SettingsStatus = $"Last saved: {DateTime.Now:HH:mm:ss}",
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    #endregion

    #region Private Helpers

    private void LoadSettings()
    {
        Console.WriteLine("[SETTINGS] 📥 LoadSettings() started");

        _currentSettings = AppSettingsService.Load();

        var settingsPath = GetSettingsPathForDebug();
        Console.WriteLine($"[SETTINGS] 📄 Config path: {settingsPath}");
        Console.WriteLine($"[SETTINGS] 📄 File exists: {File.Exists(settingsPath)}");

        if (File.Exists(settingsPath))
        {
            var content = File.ReadAllText(settingsPath);
            Console.WriteLine($"[SETTINGS] 📄 File content: {content}");
        }

        Console.WriteLine($"[SETTINGS] 🔍 Config value: StartWithWindows={_currentSettings.StartWithWindows}");

        StartWithWindows = _currentSettings.StartWithWindows;
        Console.WriteLine($"[SETTINGS] ⚙️ Field _startWithWindows set to: {_startWithWindows}");
        Console.WriteLine($"[SETTINGS] 🔔 OnPropertyChanged(nameof(StartWithWindows)) called");

        SettingsStatus = $"Loaded: {_currentSettings.LastModified:HH:mm:ss}";

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Console.WriteLine($"[SETTINGS] 🔄 Forced UI refresh, StartWithWindows={StartWithWindows}");
        });
    }

    private void OnStartWithWindowsChanged()
        => SaveSettings();

    private void ResetToDefaults()
    {
        StartWithWindows = false;
        SaveSettings();
        SettingsStatus = "Reset to defaults ✓";
    }

    #endregion
}