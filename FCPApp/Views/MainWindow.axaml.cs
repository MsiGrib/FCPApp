using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FCPApp.ViewModels;
using System;

namespace FCPApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.StopAutoRefresh();
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
            else
            {
                Console.WriteLine("[DEBUG] Folder not selected or invalid DataContext");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] OnSelectFolderClicked: {ex}");
        }
    }
}