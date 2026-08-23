using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.App.ViewModels;
using TruckNav.Connector.Capture;
using TruckNav.Connector.Networking.Protocol;

namespace TruckNav.Connector.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddTruckNavCapture();
                services.AddTruckNavNetworking();

                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IConnectorUiStateStore, FileConnectorUiStateStore>();
                services.AddSingleton<ILocalConnectorProfileService, LocalConnectorProfileService>();
                services.AddSingleton<LanPairingService>();
                services.AddSingleton<ILanPairingService>(serviceProvider =>
                    serviceProvider.GetRequiredService<LanPairingService>());
                services.AddHostedService(serviceProvider =>
                    serviceProvider.GetRequiredService<LanPairingService>());

                services.AddSingleton<HomeViewModel>();
                services.AddSingleton<FirstTimeSetupViewModel>();
                services.AddSingleton<DevicesViewModel>();
                services.AddSingleton<GamesViewModel>();
                services.AddSingleton<GpsCaptureViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<DiagnosticsViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
