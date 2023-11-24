﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

using OVRLighthouseManager.Activation;
using OVRLighthouseManager.Contracts.Services;
using OVRLighthouseManager.Core.Contracts.Services;
using OVRLighthouseManager.Core.Services;
using OVRLighthouseManager.Helpers;
using OVRLighthouseManager.Models;
using OVRLighthouseManager.Services;
using OVRLighthouseManager.ViewModels;
using OVRLighthouseManager.Views;
using Valve.VR;

namespace OVRLighthouseManager;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            services.AddSingleton<ILighthouseService, LighthouseService>();
            services.AddSingleton<ILighthouseSettingsService, LighthouseSettingsService>();
            services.AddSingleton<IOverlayAppService, OverlayAppService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        var mainContext = SynchronizationContext.Current;
        var vr = App.GetService<IOverlayAppService>();
        vr.OnVRMonitorConnected += async (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine("VRMonitorConnected");
            await OnVRLaunch();
        };
        vr.OnVRSystemQuit += (sender, args) =>
        {
            mainContext?.Post((o) =>
            {
                Task.Run(async () => {
                    System.Diagnostics.Debug.WriteLine("VRSystemQuit");
                    await OnExit();
                }).Wait();
                Exit();
            }, null);
        };

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await App.GetService<IActivationService>().ActivateAsync(args);
        var vr = App.GetService<IOverlayAppService>();
        if (vr.IsVRMonitorConnected)
        {
            await OnVRLaunch();
        }
    }

    private async Task OnVRLaunch()
    {
        var devices = App.GetService<ILighthouseSettingsService>().Devices.Where(d => d.IsManaged).ToArray();
        foreach (var device in devices)
        {
            System.Diagnostics.Debug.WriteLine($"Power On {device.Name}");
            var result = await LighthouseService.PowerOn(AddressToStringConverter.StringToAddress(device.BluetoothAddress));
            System.Diagnostics.Debug.WriteLine($"Done {device.Name}: {result}");
        }
    }

    private async Task OnExit()
    {
        App.GetService<IOverlayAppService>().Shutdown();
        await App.GetService<ILighthouseService>().StopScanAsync();
        var devices = App.GetService<ILighthouseSettingsService>().Devices.Where(d => d.IsManaged);
        foreach (var device in devices)
        {
            System.Diagnostics.Debug.WriteLine($"Sleeping {device.Name}");
            var result = await LighthouseService.Sleep(AddressToStringConverter.StringToAddress(device.BluetoothAddress));
            System.Diagnostics.Debug.WriteLine($"Done {device.Name}: {result}");
        }
    }
}
