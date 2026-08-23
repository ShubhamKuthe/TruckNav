using TruckNav.Connector.Capture;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Tests;

public sealed class GpsCaptureProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"trucknav-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Save_ShouldKeepSeparateEts2AndAtsProfiles()
    {
        var store = new JsonGpsCaptureProfileStore(_directory);

        store.Save(CreateProfile(GameType.EuroTruckSimulator2, 0.7));
        store.Save(CreateProfile(GameType.AmericanTruckSimulator, 0.6));

        Assert.Equal(2, store.GetAll().Count);
        Assert.Equal(0.7, store.Get(GameType.EuroTruckSimulator2)!.NormalizedX, 5);
        Assert.Equal(0.6, store.Get(GameType.AmericanTruckSimulator)!.NormalizedX, 5);
    }

    [Fact]
    public void Constructor_ShouldRejectMalformedProfileDocumentSafely()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "gps-capture-profiles.json"), "{not-json");

        var store = new JsonGpsCaptureProfileStore(_directory);

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Save_ShouldRejectInvalidProfile()
    {
        var store = new JsonGpsCaptureProfileStore(_directory);
        var invalid = CreateProfile(GameType.EuroTruckSimulator2, 0.9) with
        {
            NormalizedWidth = 0.2
        };

        Assert.ThrowsAny<ArgumentException>(() => store.Save(invalid));
    }

    [Fact]
    public void Save_ShouldPersistAcrossStoreRestart()
    {
        var store = new JsonGpsCaptureProfileStore(_directory);
        store.Save(CreateProfile(GameType.EuroTruckSimulator2, 0.7));

        var reloaded = new JsonGpsCaptureProfileStore(_directory);

        Assert.NotNull(reloaded.Get(GameType.EuroTruckSimulator2));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static GpsCaptureProfile CreateProfile(GameType gameType, double x)
    {
        var now = DateTimeOffset.UtcNow;
        return new GpsCaptureProfile(
            GpsCaptureProfile.CurrentSchemaVersion,
            gameType,
            CaptureSourceType.AutoWindow,
            $"game:{gameType}",
            x,
            0.6,
            0.1,
            0.2,
            1920,
            1080,
            DisplayFitMode.Fill,
            now,
            now);
    }
}
