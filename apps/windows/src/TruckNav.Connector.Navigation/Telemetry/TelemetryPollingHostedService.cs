using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Telemetry;

public sealed class TelemetryPollingHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly INavigationStateStore _stateStore;
    private readonly ITelemetryReader _telemetryReader;
    private readonly ILogger<TelemetryPollingHostedService> _logger;
    private bool _wasConnected;

    public TelemetryPollingHostedService(
        INavigationStateStore stateStore,
        ITelemetryReader telemetryReader,
        ILogger<TelemetryPollingHostedService> logger)
    {
        _stateStore = stateStore;
        _telemetryReader = telemetryReader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var connected = _telemetryReader.TryRead(out var snapshot) && snapshot.Connected;
            if (connected)
            {
                _stateStore.UpdateTelemetry(snapshot);
            }
            else if (_wasConnected || _stateStore.Snapshot.Telemetry.Connected)
            {
                _stateStore.UpdateTelemetry(TelemetrySnapshot.Disconnected with { TimestampUtc = DateTimeOffset.UtcNow });
            }

            if (connected != _wasConnected)
            {
                _logger.LogInformation(
                    connected ? "SCS telemetry shared memory connected." : "SCS telemetry shared memory disconnected.");
                _wasConnected = connected;
            }
        }
    }

    public override void Dispose()
    {
        _telemetryReader.Dispose();
        base.Dispose();
    }
}
