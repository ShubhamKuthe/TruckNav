using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruckNav.Connector.Networking.Session;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.App.Services;

public sealed class LocalConnectorProfileService : ILocalConnectorProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _identityPath;
    private readonly IConnectorSessionStore _sessionStore;

    public LocalConnectorProfileService(
        IOptions<ConnectorStorageOptions> storageOptions,
        IConnectorSessionStore sessionStore)
    {
        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("Connector storage root directory is not configured.");
        }

        Directory.CreateDirectory(rootDirectory);
        _identityPath = Path.Combine(rootDirectory, "connector-identity.json");
        _sessionStore = sessionStore;
    }

    public string DeviceName => _sessionStore.Snapshot.LocalConnector.DeviceName;

    public event EventHandler<string>? DeviceNameChanged;

    public void SaveDeviceName(string deviceName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(deviceName)
            ? "ROCK"
            : deviceName.Trim();

        var snapshot = _sessionStore.Snapshot;
        if (string.Equals(snapshot.LocalConnector.DeviceName, normalizedName, StringComparison.Ordinal))
        {
            return;
        }

        var payload = new PersistedConnectorIdentity(
            snapshot.LocalConnector.DeviceId,
            normalizedName);

        File.WriteAllText(_identityPath, JsonSerializer.Serialize(payload, JsonOptions));
        _sessionStore.UpdateLocalConnector(snapshot.LocalConnector with
        {
            DeviceName = normalizedName
        });

        DeviceNameChanged?.Invoke(this, normalizedName);
    }

    private sealed record PersistedConnectorIdentity(string DeviceId, string DeviceName);
}
