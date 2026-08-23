using TruckNav.Connector.Capture;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Tests;

public sealed class WindowsGameDetectionServiceTests
{
    [Fact]
    public async Task RefreshAsync_WithNoSupportedGames_KeepsEmptySnapshot()
    {
        var provider = new FakeProcessSnapshotProvider(Array.Empty<ProcessSnapshot>());
        var service = CreateService(provider);
        var changeCount = 0;
        service.SnapshotChanged += (_, _) => changeCount += 1;

        var snapshot = await service.RefreshAsync();

        Assert.Empty(snapshot.RunningGames);
        Assert.Equal(GameType.None, snapshot.SingleRunningGameOrNone);
        Assert.Equal(0, changeCount);
    }

    [Fact]
    public async Task RefreshAsync_DetectsEts2RunningDuringStartupWithoutWindow()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[]
            {
                new ProcessSnapshot(101, "eurotrucks2.exe", 0, "Truck window")
            });
        var service = CreateService(provider);

        var snapshot = await service.RefreshAsync();
        var game = snapshot.GetGame(GameType.EuroTruckSimulator2);

        Assert.NotNull(game);
        Assert.True(snapshot.IsRunning(GameType.EuroTruckSimulator2));
        Assert.Equal(GameType.EuroTruckSimulator2, snapshot.SingleRunningGameOrNone);
        Assert.Equal(101, game!.ProcessId);
        Assert.False(game.WindowAvailable);
    }

    [Fact]
    public async Task RefreshAsync_DetectsAtsRunningWithWindow()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[]
            {
                new ProcessSnapshot(202, "amtrucks.exe", 88, "Truck window")
            });
        var service = CreateService(provider);

        var snapshot = await service.RefreshAsync();
        var game = snapshot.GetGame(GameType.AmericanTruckSimulator);

        Assert.NotNull(game);
        Assert.True(snapshot.IsRunning(GameType.AmericanTruckSimulator));
        Assert.Equal(GameType.AmericanTruckSimulator, snapshot.SingleRunningGameOrNone);
        Assert.Equal(202, game!.ProcessId);
        Assert.True(game.WindowAvailable);
    }

    [Fact]
    public async Task RefreshAsync_DetectsBothSupportedGames()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[]
            {
                new ProcessSnapshot(101, "eurotrucks2.exe", 11, "ETS2"),
                new ProcessSnapshot(202, "amtrucks.exe", 22, "ATS")
            });
        var service = CreateService(provider);

        var snapshot = await service.RefreshAsync();

        Assert.True(snapshot.IsRunning(GameType.EuroTruckSimulator2));
        Assert.True(snapshot.IsRunning(GameType.AmericanTruckSimulator));
        Assert.Equal(GameType.None, snapshot.SingleRunningGameOrNone);
    }

    [Fact]
    public async Task RefreshAsync_EmitsLaunchAndCloseTransitions()
    {
        var provider = new FakeProcessSnapshotProvider(
            Array.Empty<ProcessSnapshot>(),
            new[] { new ProcessSnapshot(101, "eurotrucks2.exe", 11, "ETS2") },
            Array.Empty<ProcessSnapshot>());
        var service = CreateService(provider);
        var snapshots = new List<GameDetectionSnapshot>();
        service.SnapshotChanged += (_, snapshot) => snapshots.Add(snapshot);

        await service.RefreshAsync();
        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Equal(2, snapshots.Count);
        Assert.True(snapshots[0].IsRunning(GameType.EuroTruckSimulator2));
        Assert.Empty(snapshots[1].RunningGames);
    }

    [Fact]
    public async Task RefreshAsync_StateUnchanged_DoesNotEmitDuplicateEvent()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[] { new ProcessSnapshot(202, "amtrucks.exe", 44, "ATS") },
            new[] { new ProcessSnapshot(202, "amtrucks.exe", 44, "ATS") });
        var service = CreateService(provider);
        var changeCount = 0;
        service.SnapshotChanged += (_, _) => changeCount += 1;

        await service.RefreshAsync();
        await service.RefreshAsync();

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task RefreshAsync_DetectsRestartedProcessInstance()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[] { new ProcessSnapshot(101, "eurotrucks2.exe", 11, "ETS2") },
            new[] { new ProcessSnapshot(303, "eurotrucks2.exe", 11, "ETS2") });
        var service = CreateService(provider);

        await service.RefreshAsync();
        var snapshot = await service.RefreshAsync();

        Assert.Equal(303, snapshot.GetGame(GameType.EuroTruckSimulator2)!.ProcessId);
    }

    [Fact]
    public async Task RefreshAsync_TracksWindowAvailabilitySeparatelyFromProcessRunning()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[] { new ProcessSnapshot(101, "eurotrucks2.exe", 0, null) },
            new[] { new ProcessSnapshot(101, "eurotrucks2.exe", 77, "ETS2") });
        var service = CreateService(provider);
        var snapshots = new List<GameDetectionSnapshot>();
        service.SnapshotChanged += (_, snapshot) => snapshots.Add(snapshot);

        var firstSnapshot = await service.RefreshAsync();
        var secondSnapshot = await service.RefreshAsync();

        Assert.True(firstSnapshot.IsRunning(GameType.EuroTruckSimulator2));
        Assert.False(firstSnapshot.GetGame(GameType.EuroTruckSimulator2)!.WindowAvailable);
        Assert.True(secondSnapshot.IsRunning(GameType.EuroTruckSimulator2));
        Assert.True(secondSnapshot.GetGame(GameType.EuroTruckSimulator2)!.WindowAvailable);
        Assert.Equal(2, snapshots.Count);
    }

    [Fact]
    public async Task RefreshAsync_IgnoresUnsupportedOrMalformedProcessData()
    {
        var provider = new FakeProcessSnapshotProvider(
            new[]
            {
                new ProcessSnapshot(1, "notepad.exe", 0, "Notepad"),
                new ProcessSnapshot(2, "   ", 0, null),
                new ProcessSnapshot(3, null!, 0, null)
            });
        var service = CreateService(provider);

        var snapshot = await service.RefreshAsync();

        Assert.Empty(snapshot.RunningGames);
    }

    private static WindowsGameDetectionService CreateService(IProcessSnapshotProvider provider) =>
        new(
            provider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsGameDetectionService>.Instance);

    private sealed class FakeProcessSnapshotProvider : IProcessSnapshotProvider
    {
        private readonly Queue<IReadOnlyList<ProcessSnapshot>> _snapshots;
        private IReadOnlyList<ProcessSnapshot> _lastSnapshot = Array.Empty<ProcessSnapshot>();

        public FakeProcessSnapshotProvider(params IReadOnlyList<ProcessSnapshot>[] snapshots)
        {
            _snapshots = new Queue<IReadOnlyList<ProcessSnapshot>>(snapshots);
        }

        public Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(CancellationToken cancellationToken = default)
        {
            if (_snapshots.Count > 0)
            {
                _lastSnapshot = _snapshots.Dequeue();
            }

            return Task.FromResult(_lastSnapshot);
        }
    }
}
