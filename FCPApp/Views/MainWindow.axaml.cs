using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FCPApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FCPApp.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _mainVm;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _mainVm = viewModel;

        this.Closed += MainWindow_Closed;
    }

    public MainWindow() : this(App.Services.GetRequiredService<MainViewModel>()) { }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _mainVm?.OnWindowClosing();
        Console.WriteLine("[UI] Window closed");
    }

    private async void OnSelectFolderClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the root folder",
                AllowMultiple = false
            });

            if (folders.Count > 0 && DataContext is MainViewModel vm)
            {
                var path = folders[0].Path.LocalPath;
                Console.WriteLine($"[DEBUG] Folder selected: {path}");
                vm.SetRootPath(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] OnSelectFolderClicked: {ex}");
        }
    }
}