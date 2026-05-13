using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FCPApp.Services.FileSystem;
using FCPApp.Services.Refresh;
using FCPApp.Services.Selection;
using FCPApp.Services.Tree;
using FCPApp.ViewModels;
using FCPApp.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace FCPApp
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
            => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            var services = new ServiceCollection();
            RegisterServices(services);
            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                desktop.MainWindow = new MainWindow(Services.GetRequiredService<MainViewModel>());
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void RegisterServices(ServiceCollection services)
        {
            services.AddSingleton<IFileSystemService, FileSystemService>();
            services.AddSingleton<IFolderTreeService, FolderTreeService>();
            services.AddSingleton<ISelectionManager, SelectionManager>();
            services.AddSingleton<IAutoRefreshManager, AutoRefreshManager>();

            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var plugins = BindingPlugins.DataValidators
                .OfType<DataAnnotationsValidationPlugin>()
                .ToArray();

            foreach (var p in plugins)
                BindingPlugins.DataValidators.Remove(p);
        }
    }
}