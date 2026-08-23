using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Utilities;

public static class StreamStateResolver
{
    public static StreamState Resolve(
        DeviceConnectionState deviceState,
        GameType gameType,
        bool hasCaptureProfile,
        bool isCaptureAvailable,
        bool userStopped,
        bool isStreaming)
    {
        if (userStopped)
        {
            return StreamState.StoppedByUser;
        }

        if (deviceState != DeviceConnectionState.Connected)
        {
            return deviceState == DeviceConnectionState.Reconnecting
                ? StreamState.Reconnecting
                : StreamState.WaitingForDevice;
        }

        if (gameType == GameType.None)
        {
            return StreamState.WaitingForGame;
        }

        if (!hasCaptureProfile)
        {
            return StreamState.WaitingForCaptureProfile;
        }

        if (!isCaptureAvailable)
        {
            return StreamState.CaptureUnavailable;
        }

        return isStreaming ? StreamState.Streaming : StreamState.Starting;
    }
}
