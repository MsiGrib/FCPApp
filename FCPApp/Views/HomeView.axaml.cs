using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FCPApp.ViewModels;
using System;

namespace FCPApp.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private async void OnSelectFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (VisualRoot is Window mainWindow &&
            mainWindow.DataContext is MainViewModel vm)
        {
            try
            {
                var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title = "Select the root folder",
                        AllowMultiple = false
                    });

                if (folders.Count > 0)
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
}