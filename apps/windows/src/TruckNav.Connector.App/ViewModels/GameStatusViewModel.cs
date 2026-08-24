using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class GameStatusViewModel : ObservableObject
{
    private readonly IGameInstallationLocator _installationLocator;
    private readonly ITelemetryPluginInstaller _pluginInstaller;
    private readonly INavigationDataBuilder _dataBuilder;

    public GameStatusViewModel(
        GameType gameType,
        IGameInstallationLocator installationLocator,
        ITelemetryPluginInstaller pluginInstaller,
        INavigationDataBuilder dataBuilder)
    {
        GameType = gameType;
        Name = SupportedGameCatalog.GetDisplayName(gameType);
        _installationLocator = installationLocator;
        _pluginInstaller = pluginInstaller;
        _dataBuilder = dataBuilder;

        InstallPluginCommand = new AsyncRelayCommand(InstallPluginAsync, () => IsInstalled && !IsBusy);
        BuildNavigationDataCommand = new AsyncRelayCommand(BuildNavigationDataAsync, () => IsInstalled && !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SelectGameFolderCommand = new AsyncRelayCommand(SelectGameFolderAsync, () => !IsBusy);
    }

    public GameType GameType { get; }

    public string Name { get; }

    public IAsyncRelayCommand InstallPluginCommand { get; }

    public IAsyncRelayCommand BuildNavigationDataCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand SelectGameFolderCommand { get; }

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string installDetails = "Detecting Steam installation...";

    [ObservableProperty]
    private string pluginStatus = "Checking telemetry plugin...";

    [ObservableProperty]
    private string navigationDataStatus = "Checking local graph...";

    [ObservableProperty]
    private string operationMessage = string.Empty;

    [ObservableProperty]
    private StatusTone pluginTone = StatusTone.Neutral;

    [ObservableProperty]
    private StatusTone navigationDataTone = StatusTone.Neutral;

    public async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            var installation = await Task.Run(() =>
                _installationLocator.FindInstallations().FirstOrDefault(value => value.Game == GameType));
            IsInstalled = installation is not null;
            InstallDetails = installation is null
                ? "Not found in Steam libraries. Configure a manual path in %LOCALAPPDATA%\\TruckNav\\game-paths.json."
                : $"{installation.Version}  |  {installation.InstallPath}";

            var plugin = await Task.Run(() => _pluginInstaller.GetStatus(GameType));
            PluginStatus = plugin.Message;
            PluginTone = plugin.PluginCurrent
                ? StatusTone.Success
                : plugin.GameInstalled
                    ? StatusTone.Warning
                    : StatusTone.Neutral;

            var data = await _dataBuilder.GetStatusAsync(GameType);
            NavigationDataStatus = data.IsReady
                ? $"Ready: {data.NodeCount:N0} nodes, {data.EdgeCount:N0} edges. {data.Message}"
                : data.Message;
            NavigationDataTone = data.IsReady ? StatusTone.Success : IsInstalled ? StatusTone.Warning : StatusTone.Neutral;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InstallPluginAsync()
    {
        SetBusy(true);
        OperationMessage = "Installing the verified telemetry plugin...";
        try
        {
            var result = await _pluginInstaller.InstallAsync(GameType);
            OperationMessage = result.Message;
        }
        finally
        {
            SetBusy(false);
        }

        await RefreshAsync();
    }

    private async Task BuildNavigationDataAsync()
    {
        SetBusy(true);
        OperationMessage = "Generating navigation data locally. This can take several minutes...";
        try
        {
            var result = await _dataBuilder.BuildAsync(GameType);
            OperationMessage = result.Message;
        }
        finally
        {
            SetBusy(false);
        }

        await RefreshAsync();
    }

    private async Task SelectGameFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Select the {Name} installation folder",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        OperationMessage = _installationLocator.TrySetManualPath(GameType, dialog.FolderName, out var error)
            ? "Manual game folder saved."
            : error;
        await RefreshAsync();
    }

    private void SetBusy(bool value)
    {
        IsBusy = value;
        InstallPluginCommand.NotifyCanExecuteChanged();
        BuildNavigationDataCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        SelectGameFolderCommand.NotifyCanExecuteChanged();
    }
}
