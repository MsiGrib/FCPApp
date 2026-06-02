using Avalonia.Controls;
using Avalonia.Input;
using FCPApp.ViewModels;

namespace FCPApp.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void NewProfileTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is HomeViewModel vm)
        {
            if (vm.CreateProfileCommand.CanExecute(null))
            {
                vm.CreateProfileCommand.Execute(null);
            }
        }
    }
}