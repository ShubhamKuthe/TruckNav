using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class FirstTimeSetupViewModel : PageViewModelBase
{
    private readonly ILocalConnectorProfileService _profileService;
    private readonly IConnectorUiStateStore _uiStateStore;
    private readonly INavigationService _navigationService;

    public FirstTimeSetupViewModel(
        ILocalConnectorProfileService profileService,
        IConnectorUiStateStore uiStateStore,
        INavigationService navigationService)
        : base(PageId.FirstTimeSetup, "Welcome to TruckNav Connector")
    {
        _profileService = profileService;
        _uiStateStore = uiStateStore;
        _navigationService = navigationService;
        ConnectorName = profileService.DeviceName;
    }

    [ObservableProperty]
    private string connectorName = "ROCK";

    [ObservableProperty]
    private int currentStep;

    public bool IsNameStep => CurrentStep == 0;

    public bool IsPermissionStep => CurrentStep == 1;

    partial void OnConnectorNameChanged(string value) => ContinueCommand.NotifyCanExecuteChanged();

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsNameStep));
        OnPropertyChanged(nameof(IsPermissionStep));
        ContinueCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue()
    {
        if (CurrentStep == 0)
        {
            _profileService.SaveDeviceName(ConnectorName);
            CurrentStep = 1;
            return;
        }

        _uiStateStore.SaveState(_uiStateStore.CurrentState with
        {
            HasCompletedOnboarding = true
        });
        _navigationService.NavigateTo(PageId.Home);
    }

    private bool CanContinue() =>
        CurrentStep > 0 || !string.IsNullOrWhiteSpace(ConnectorName);
}
