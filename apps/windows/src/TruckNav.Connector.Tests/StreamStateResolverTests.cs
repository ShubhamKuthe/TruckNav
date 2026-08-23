using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Tests;

public sealed class StreamStateResolverTests
{
    [Fact]
    public void Resolve_ShouldPreferStoppedByUser()
    {
        var result = StreamStateResolver.Resolve(
            DeviceConnectionState.Connected,
            GameType.EuroTruckSimulator2,
            hasCaptureProfile: true,
            isCaptureAvailable: true,
            userStopped: true,
            isStreaming: true);

        Assert.Equal(StreamState.StoppedByUser, result);
    }

    [Fact]
    public void Resolve_ShouldWaitForDevice_WhenPhoneIsMissing()
    {
        var result = StreamStateResolver.Resolve(
            DeviceConnectionState.WaitingForDevice,
            GameType.EuroTruckSimulator2,
            hasCaptureProfile: true,
            isCaptureAvailable: true,
            userStopped: false,
            isStreaming: false);

        Assert.Equal(StreamState.WaitingForDevice, result);
    }

    [Fact]
    public void Resolve_ShouldReturnWaitingForCaptureProfile_WhenGameRunsButNoProfileExists()
    {
        var result = StreamStateResolver.Resolve(
            DeviceConnectionState.Connected,
            GameType.EuroTruckSimulator2,
            hasCaptureProfile: false,
            isCaptureAvailable: true,
            userStopped: false,
            isStreaming: false);

        Assert.Equal(StreamState.WaitingForCaptureProfile, result);
    }
}
