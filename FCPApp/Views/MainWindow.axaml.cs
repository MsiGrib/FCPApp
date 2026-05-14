using Avalonia.Controls;
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
}