using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.Networking.Identity;

public sealed class FileConnectorIdentityProvider : IConnectorIdentityProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger<FileConnectorIdentityProvider> _logger;
    private readonly string _identityPath;
    private readonly Lazy<ConnectorPeerInfo> _identity;

    public FileConnectorIdentityProvider(
        IOptions<ConnectorStorageOptions> storageOptions,
        ILogger<FileConnectorIdentityProvider> logger)
    {
        _logger = logger;

        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("Connector storage root directory is not configured.");
        }

        _identityPath = Path.Combine(rootDirectory, "connector-identity.json");
        _identity = new Lazy<ConnectorPeerInfo>(LoadOrCreateIdentity, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ConnectorPeerInfo GetIdentity() => _identity.Value;

    private ConnectorPeerInfo LoadOrCreateIdentity()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_identityPath)!);

        if (File.Exists(_identityPath))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<PersistedConnectorIdentity>(
                    File.ReadAllText(_identityPath),
                    JsonOptions);

                if (persisted is not null &&
                    Guid.TryParse(persisted.DeviceId, out _) &&
                    !string.IsNullOrWhiteSpace(persisted.DeviceName))
                {
                    return CreateIdentity(persisted.DeviceId, persisted.DeviceName.Trim());
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to read Connector identity from {IdentityPath}. A new identity will be generated.",
                    _identityPath);
            }
        }

        var newIdentity = CreateIdentity(Guid.NewGuid().ToString("D"), "ROCK");
        var payload = new PersistedConnectorIdentity(newIdentity.DeviceId, newIdentity.DeviceName);
        File.WriteAllText(_identityPath, JsonSerializer.Serialize(payload, JsonOptions));
        _logger.LogInformation("Created a new TruckNav Connector identity at {IdentityPath}.", _identityPath);

        return newIdentity;
    }

    private static ConnectorPeerInfo CreateIdentity(string deviceId, string deviceName) =>
        new(
            ProtocolCompatibility.CurrentVersion,
            TruckNavNetworkDefaults.AppVersion,
            deviceId,
            deviceName,
            TruckNavNetworkDefaults.ConnectorRole,
            TruckNavNetworkDefaults.WindowsPlatform,
            TruckNavNetworkDefaults.ConnectorCapabilities);

    private sealed record PersistedConnectorIdentity(string DeviceId, string DeviceName);
}
