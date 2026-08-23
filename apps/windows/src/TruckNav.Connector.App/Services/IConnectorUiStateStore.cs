namespace TruckNav.Connector.App.Services;

public interface IConnectorUiStateStore
{
    ConnectorUiState CurrentState { get; }

    event EventHandler<ConnectorUiState>? StateChanged;

    void SaveState(ConnectorUiState state);
}
