using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Jadwal.Application.Interfaces;
using Jadwal.Application.Services;
using Jadwal.Infrastructure.Migration;
using Jadwal.Infrastructure.Persistence;
using Jadwal.Integrations.Jamia;
using Jadwal.Platform.MacOS.Services;
using Jadwal.Platform.Windows.Services;
using Jadwal.UI.ViewModels;

namespace Jadwal.App;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        var savedTheme = themeService.GetSavedTheme();
        RequestedThemeVariant = savedTheme switch
        {
            JadwalThemeMode.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Light
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
            await mainVm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Persistence
        services.AddSingleton<ITaskRepository, JsonFileTaskRepository>();
        services.AddSingleton<ITimetableRepository, JsonFileTimetableRepository>();
        services.AddSingleton<IChangeRepository, JsonFileChangeRepository>();
        services.AddSingleton<IProgramRepository, JsonFileProgramRepository>();
        services.AddSingleton<INoteRepository, JsonFileNoteRepository>();
        services.AddSingleton<IMiqaatRepository, JsonMiqaatRepository>();

        // Time Provider
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        // Platform Adapters
        if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<ISecureStorage, MacSecureStorage>();
            services.AddSingleton<INotificationService, MacNotificationService>();
            services.AddSingleton<IStartupService, MacStartupService>();
        }
        else
        {
            services.AddSingleton<ISecureStorage, WindowsSecureStorage>();
            services.AddSingleton<INotificationService, WindowsNotificationService>();
            services.AddSingleton<IStartupService, WindowsStartupService>();
        }

        // Integration & Migration
        services.AddSingleton<IJamiaTimetableProvider, JamiaTimetableProvider>();
        services.AddSingleton<ILegacyMigrationService, LegacyDataMigrator>();

        // Application Services
        services.AddSingleton<ThemeService>();
        services.AddSingleton<TaskService>();
        services.AddSingleton<TimetableService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<ReminderService>();
        services.AddSingleton<CalendarService>();

        // ViewModels
        services.AddTransient<TodayViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<TimetableViewModel>();
        services.AddTransient<TasksViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MenuBarViewModel>();
        services.AddSingleton<MainViewModel>();
    }

    public void OnOpenMainWindowClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                var mainVm = _serviceProvider?.GetRequiredService<MainViewModel>()
                    ?? throw new InvalidOperationException("ServiceProvider not initialized");
                desktop.MainWindow = new MainWindow(mainVm);
            }
            desktop.MainWindow.Show();
            desktop.MainWindow.Activate();
        }
    }

    public void OnQuitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}