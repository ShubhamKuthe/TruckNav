using Microsoft.Extensions.DependencyInjection;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Navigation.Data;
using TruckNav.Connector.Navigation.Detection;
using TruckNav.Connector.Navigation.Map;
using TruckNav.Connector.Navigation.Routing;
using TruckNav.Connector.Navigation.Setup;
using TruckNav.Connector.Navigation.State;
using TruckNav.Connector.Navigation.Telemetry;

namespace TruckNav.Connector.Navigation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTruckNavNavigation(this IServiceCollection services)
    {
        services.AddSingleton<INavigationStateStore, NavigationStateStore>();
        services.AddSingleton<IProcessSnapshotProvider, SystemProcessSnapshotProvider>();
        services.AddSingleton<WindowsGameDetectionService>();
        services.AddSingleton<IGameDetectionService>(provider => provider.GetRequiredService<WindowsGameDetectionService>());
        services.AddHostedService(provider => provider.GetRequiredService<WindowsGameDetectionService>());
        services.AddSingleton<ITelemetryReader, ScsTelemetrySharedMemoryReader>();
        services.AddSingleton<IGameInstallationLocator, SteamGameInstallationLocator>();
        services.AddSingleton<ITelemetryPluginInstaller, TelemetryPluginInstaller>();

        services.AddSingleton<NavigationStoragePaths>();
        services.AddSingleton<TruckermudgeonGraphImporter>();
        services.AddSingleton<IRoadGraphRepository, LocalRoadGraphRepository>();
        services.AddSingleton<IRoadGraphProvider, RoadGraphProvider>();
        services.AddSingleton<INavigationDataBuilder, ExternalNavigationDataBuilder>();
        services.AddSingleton<VisualMapStoragePaths>();
        services.AddSingleton<VisualMapDatasetBuilder>();
        services.AddSingleton<IVisualMapService, VisualMapService>();

        services.AddSingleton<IDestinationResolver, ExactDestinationResolver>();
        services.AddSingleton<IRoutePlanner, AStarRoutePlanner>();
        services.AddSingleton<IRouteProgressTracker, RouteProgressTracker>();
        services.AddSingleton<IGameCoordinateTransform, GameCoordinateTransform>();

        services.AddHostedService<TelemetryPollingHostedService>();
        services.AddHostedService<NavigationOrchestratorHostedService>();
        return services;
    }
}
