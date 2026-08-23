namespace TruckNav.Connector.App.Services;

public interface ILocalConnectorProfileService
{
    string DeviceName { get; }

    event EventHandler<string>? DeviceNameChanged;

    void SaveDeviceName(string deviceName);
}
