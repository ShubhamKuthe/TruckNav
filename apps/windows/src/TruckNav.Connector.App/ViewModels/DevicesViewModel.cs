using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed class DevicesViewModel : InfoPageViewModel
{
    private readonly IConnectorSessionStore _sessionStore;

    public DevicesViewModel(IConnectorSessionStore sessionStore)
        : base(
            PageId.Devices,
            "Devices",
            "Known mobile devices, trust relationships, and active-client constraints live here.")
    {
        _sessionStore = sessionStore;
        RefreshSections(_sessionStore.Snapshot);
        _sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() => RefreshSections(snapshot));
    }

    private void RefreshSections(ConnectorSessionSnapshot snapshot)
    {
        Sections.Clear();

        var peer = snapshot.ActiveMobileDevice ?? snapshot.LastSeenMobileDevice;
        var peerItems = peer is null
            ? (IReadOnlyList<string>)
            [
                "- No Android client has completed a hello handshake yet.",
                "- Discovery still relies on mDNS and persistent UUID identity rather than typed IP addresses."
            ]
            : [
                $"- Device: {peer.DeviceName}",
                $"- Platform: {peer.Platform}",
                $"- Protocol version: {peer.ProtocolVersion}",
                $"- Status: {GetDeviceStatusLabel(snapshot)}"
            ];

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Connected devices",
            Description = "This snapshot is driven by the Phase 2 control session, not placeholder shell text.",
            Items = peerItems
        });

        Sections.Add(new PageSectionViewModel
        {
            Heading = "Active client model",
            Description = "V1 allows one active mobile control session even though discovery is designed around stable device identity.",
            Items =
            [
                "- A second client receives a typed busy response instead of replacing the active session.",
                "- Intentional disconnect returns the connector to waiting state and does not imply reconnecting.",
                "- Unexpected disconnects move the connector into reconnecting so the UI can stay honest."
            ]
        });

        if (!string.IsNullOrWhiteSpace(snapshot.LastIssueCode))
        {
            Sections.Add(new PageSectionViewModel
            {
                Heading = "Most recent protocol issue",
                Description = "Validation and compatibility errors are surfaced explicitly instead of failing silently.",
                Items =
                [
                    $"- Code: {snapshot.LastIssueCode}",
                    $"- Detail: {snapshot.LastIssueMessage}"
                ]
            });
        }
    }

    private static string GetDeviceStatusLabel(ConnectorSessionSnapshot snapshot) =>
        snapshot.DeviceState switch
        {
            DeviceConnectionState.Connected => "Connected",
            DeviceConnectionState.Reconnecting => "Reconnecting",
            _ => "Waiting for reconnect"
        };

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.Invoke(action);
    }
}
