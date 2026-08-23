using Microsoft.Extensions.DependencyInjection;
using TruckNav.Connector.Core.Interfaces;

namespace TruckNav.Connector.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTruckNavCapture(this IServiceCollection services)
    {
        services.AddSingleton<IProcessSnapshotProvider, SystemProcessSnapshotProvider>();
        services.AddSingleton<WindowsGameDetectionService>();
        services.AddSingleton<IGameDetectionService>(serviceProvider =>
            serviceProvider.GetRequiredService<WindowsGameDetectionService>());
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<WindowsGameDetectionService>());
        services.AddSingleton<IWindowsMonitorProvider, WindowsMonitorProvider>();
        services.AddSingleton<IGpsCapturePresetProvider, StandardGpsCapturePresetProvider>();
        services.AddSingleton<IGameCaptureService, WindowsGraphicsCaptureService>();
        return services;
    }
}
