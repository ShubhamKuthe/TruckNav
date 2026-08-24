using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Tests;

public sealed class ProtocolSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripHelloMessage()
    {
        var message = new HelloMessage(
            ProtocolVersion: ProtocolCompatibility.CurrentVersion,
            AppVersion: "0.1.0",
            DeviceId: "7ac8fc8b-9fd6-4acf-a451-e0c4d3b5a920",
            DeviceName: "HEN",
            Role: "mobile",
            Platform: "android",
            Capabilities: Array.Empty<string>());

        var json = ProtocolSerializer.Serialize(message);
        var result = ProtocolSerializer.Deserialize<HelloMessage>(json);

        Assert.Equal(message.Type, result.Type);
        Assert.Equal(message.ProtocolVersion, result.ProtocolVersion);
        Assert.Equal(message.AppVersion, result.AppVersion);
        Assert.Equal(message.DeviceId, result.DeviceId);
        Assert.Equal(message.DeviceName, result.DeviceName);
        Assert.Equal(message.Role, result.Role);
        Assert.Equal(message.Platform, result.Platform);
        Assert.Equal(message.Capabilities, result.Capabilities);
    }

    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripGameStateMessage()
    {
        var message = new GameStateMessage(
            new[]
            {
                new GameStateEntryMessage("ets2", true),
                new GameStateEntryMessage("ats", false)
            });

        var json = ProtocolSerializer.Serialize(message);
        var result = ProtocolSerializer.Deserialize<GameStateMessage>(json);

        Assert.Equal(message.Type, result.Type);
        Assert.Collection(
            result.Games,
            game =>
            {
                Assert.Equal("ets2", game.Game);
                Assert.True(game.Running);
            },
            game =>
            {
                Assert.Equal("ats", game.Game);
                Assert.False(game.Running);
            });
    }

    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripNativeNavigationMessages()
    {
        var telemetry = new TelemetryStateMessage(
            "ets2", true, false, 12.5, -7.5, 30, 90, 80, 90, true,
            "Berlin", "Transinet", 1_200, 60, "Navigating", "Turn right", DateTimeOffset.UtcNow);
        var route = new RouteStateMessage(
            4, true, "route-1", "ets2", "Transinet, Berlin",
            [new RoutePointMessage(0, 0), new RoutePointMessage(100, 0)],
            [new RouteStepMessage("Arrive", "Arrive at destination", 1, 100)],
            [new RouteContextRoadMessage(0, 0, 100, 0, true, false)],
            [new RouteMapPointMessage("Fuel", 50, 10, null, false)],
            [new RouteMapLabelMessage("Berlin", 100, 0, true)],
            100, 10, DateTimeOffset.UtcNow, false);
        var progress = new RouteProgressMessage(
            "route-1", 0, 20, 0, 1, 80, 8, "Arrive", "Arrive at destination", 80, false, false,
            DateTimeOffset.UtcNow);

        var telemetryResult = ProtocolSerializer.Deserialize<TelemetryStateMessage>(ProtocolSerializer.Serialize(telemetry));
        var routeResult = ProtocolSerializer.Deserialize<RouteStateMessage>(ProtocolSerializer.Serialize(route));
        var progressResult = ProtocolSerializer.Deserialize<RouteProgressMessage>(ProtocolSerializer.Serialize(progress));

        Assert.Equal(ProtocolMessageType.TelemetryState, telemetryResult.Type);
        Assert.Equal(12.5, telemetryResult.X);
        Assert.Equal(ProtocolMessageType.RouteState, routeResult.Type);
        Assert.Equal(2, routeResult.Points.Count);
        Assert.Equal(ProtocolMessageType.RouteProgress, progressResult.Type);
        Assert.Equal(80, progressResult.RemainingDistanceMeters);
    }
}
