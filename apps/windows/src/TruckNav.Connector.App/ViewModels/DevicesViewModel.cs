using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class DevicesViewModel : PageViewModelBase
{
    private readonly RelayCommand _disabledCommand = new(() => { });
    private readonly ILanPairingService _pairingService;
    private readonly HashSet<string> _pendingPairRequests = new(StringComparer.OrdinalIgnoreCase);
    private ConnectorSessionSnapshot _sessionSnapshot;

    public DevicesViewModel(
        IConnectorSessionStore sessionStore,
        ILanPairingService pairingService)
        : base(PageId.Devices, "Devices")
    {
        _pairingService = pairingService;
        _sessionSnapshot = sessionStore.Snapshot;

        ConnectedDevices = new ObservableCollection<DeviceEntryViewModel>();
        NearbyDevices = new ObservableCollection<DeviceEntryViewModel>();
        PreviousDevices = new ObservableCollection<DeviceEntryViewModel>();

        RefreshSections();
        sessionStore.SnapshotChanged += (_, snapshot) => Dispatch(() =>
        {
            _sessionSnapshot = snapshot;
            if (snapshot.ActiveMobileDevice is not null)
            {
                _pendingPairRequests.Remove(snapshot.ActiveMobileDevice.DeviceId);
            }

            RefreshSections();
        });
        pairingService.MobileDevicesChanged += (_, _) => Dispatch(RefreshSections);
    }

    public ObservableCollection<DeviceEntryViewModel> ConnectedDevices { get; }

    public ObservableCollection<DeviceEntryViewModel> NearbyDevices { get; }

    public ObservableCollection<DeviceEntryViewModel> PreviousDevices { get; }

    [ObservableProperty]
    private bool hasConnectedDevices;

    [ObservableProperty]
    private bool hasNearbyDevices;

    [ObservableProperty]
    private bool hasPreviousDevices;

    private void RefreshSections()
    {
        ConnectedDevices.Clear();
        NearbyDevices.Clear();
        PreviousDevices.Clear();

        var snapshot = _sessionSnapshot;
        var activeMobileId = snapshot.ActiveMobileDevice?.DeviceId;
        var nearbyMobiles = _pairingService.MobileDevices;

        if (snapshot.ActiveMobileDevice is not null)
        {
            ConnectedDevices.Add(new DeviceEntryViewModel
            {
                Name = snapshot.ActiveMobileDevice.DeviceName,
                Subtitle = $"{FormatPlatform(snapshot.ActiveMobileDevice.Platform)} · Paired with this PC",
                StatusText = "Paired",
                Tone = StatusTone.Success
            });
        }

        foreach (var mobileDevice in nearbyMobiles)
        {
            if (string.Equals(mobileDevice.DeviceId, activeMobileId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isPending = _pendingPairRequests.Contains(mobileDevice.DeviceId);
            NearbyDevices.Add(new DeviceEntryViewModel
            {
                Name = mobileDevice.DeviceName,
                Subtitle = $"{FormatPlatform(mobileDevice.Platform)} · Visible on the local network",
                StatusText = isPending ? "Pair request sent" : "Ready to pair",
                Tone = isPending ? StatusTone.Accent : StatusTone.Success,
                ActionText = isPending ? "Waiting..." : "Connect",
                ActionCommand = isPending
                    ? _disabledCommand
                    : new AsyncRelayCommand(() => RequestPairAsync(mobileDevice.DeviceId)),
                IsActionEnabled = !isPending,
                IsPrimaryAction = !isPending
            });
        }

        if (snapshot.ActiveMobileDevice is null &&
            snapshot.LastSeenMobileDevice is not null &&
            !nearbyMobiles.Any(device =>
                string.Equals(device.DeviceId, snapshot.LastSeenMobileDevice.DeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            PreviousDevices.Add(new DeviceEntryViewModel
            {
                Name = snapshot.LastSeenMobileDevice.DeviceName,
                Subtitle = snapshot.DeviceState == DeviceConnectionState.Reconnecting
                    ? $"{FormatPlatform(snapshot.LastSeenMobileDevice.Platform)} · Waiting to pair again"
                    : $"{FormatPlatform(snapshot.LastSeenMobileDevice.Platform)} · Seen earlier on this PC",
                StatusText = snapshot.DeviceState == DeviceConnectionState.Reconnecting
                    ? "Trying again"
                    : "Previous device",
                Tone = snapshot.DeviceState == DeviceConnectionState.Reconnecting
                    ? StatusTone.Accent
                    : StatusTone.Neutral
            });
        }

        HasConnectedDevices = ConnectedDevices.Count > 0;
        HasNearbyDevices = NearbyDevices.Count > 0;
        HasPreviousDevices = PreviousDevices.Count > 0;
    }

    private async Task RequestPairAsync(string mobileDeviceId)
    {
        if (!_pendingPairRequests.Add(mobileDeviceId))
        {
            return;
        }

        RefreshSections();

        try
        {
            await _pairingService.RequestPairAsync(mobileDeviceId);
            _ = ClearPendingRequestAsync(mobileDeviceId);
        }
        catch
        {
            _pendingPairRequests.Remove(mobileDeviceId);
            RefreshSections();
            throw;
        }
    }

    private async Task ClearPendingRequestAsync(string mobileDeviceId)
    {
        await Task.Delay(TimeSpan.FromSeconds(8));
        Dispatch(() =>
        {
            if (_sessionSnapshot.ActiveMobileDevice?.DeviceId == mobileDeviceId)
            {
                return;
            }

            _pendingPairRequests.Remove(mobileDeviceId);
            RefreshSections();
        });
    }

    private static string FormatPlatform(string platform) =>
        string.IsNullOrWhiteSpace(platform)
            ? "Android"
            : char.ToUpperInvariant(platform[0]) + platform[1..].ToLowerInvariant();

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
