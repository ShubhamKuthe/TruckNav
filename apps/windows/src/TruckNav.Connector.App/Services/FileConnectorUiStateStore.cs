using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.App.Services;

public sealed class FileConnectorUiStateStore : IConnectorUiStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _statePath;

    public FileConnectorUiStateStore(IOptions<ConnectorStorageOptions> storageOptions)
    {
        var rootDirectory = storageOptions.Value.RootDirectory;
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("Connector storage root directory is not configured.");
        }

        Directory.CreateDirectory(rootDirectory);
        _statePath = Path.Combine(rootDirectory, "connector-ui-state.json");
        CurrentState = LoadState();
    }

    public ConnectorUiState CurrentState { get; private set; }

    public event EventHandler<ConnectorUiState>? StateChanged;

    public void SaveState(ConnectorUiState state)
    {
        lock (_gate)
        {
            CurrentState = state;
            File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonOptions));
        }

        StateChanged?.Invoke(this, state);
    }

    private ConnectorUiState LoadState()
    {
        if (!File.Exists(_statePath))
        {
            return new ConnectorUiState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<ConnectorUiState>(
                File.ReadAllText(_statePath),
                JsonOptions);

            return state ?? new ConnectorUiState();
        }
        catch
        {
            return new ConnectorUiState();
        }
    }
}
