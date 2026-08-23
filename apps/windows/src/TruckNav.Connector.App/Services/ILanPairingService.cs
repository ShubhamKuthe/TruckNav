namespace TruckNav.Connector.App.Services;

public interface ILanPairingService
{
    IReadOnlyList<LanMobileDevice> MobileDevices { get; }

    event EventHandler<IReadOnlyList<LanMobileDevice>>? MobileDevicesChanged;

    Task RequestPairAsync(string mobileDeviceId, CancellationToken cancellationToken = default);
}
