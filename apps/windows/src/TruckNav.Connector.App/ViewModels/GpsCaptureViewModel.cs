using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.App.Services;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.App.ViewModels;

public sealed partial class GpsCaptureViewModel : PageViewModelBase
{
    private readonly IGameDetectionService _gameDetectionService;
    private readonly IGameCaptureService _captureService;
    private readonly IGpsCaptureProfileStore _profileStore;
    private readonly IGpsCapturePresetProvider _presetProvider;
    private readonly INavigationService _navigationService;
    private readonly ILogger<GpsCaptureViewModel> _logger;
    private readonly ResolutionChangeTracker _resolutionChangeTracker = new();

    private readonly LatestValueBuffer<VideoFrame> _pendingUiFrames = new();
    private VideoFrame? _lastRenderedFrame;
    private WriteableBitmap? _fullPreviewBitmap;
    private WriteableBitmap? _cropPreviewBitmap;
    private int _uiFrameScheduled;
    private bool _selectionInitialized;
    private GpsCaptureProfile? _activeProfile;
    private int _dismissedResolutionWidth;
    private int _dismissedResolutionHeight;

    public GpsCaptureViewModel(
        IGameDetectionService gameDetectionService,
        IGameCaptureService captureService,
        IGpsCaptureProfileStore profileStore,
        IGpsCapturePresetProvider presetProvider,
        INavigationService navigationService,
        ILogger<GpsCaptureViewModel> logger)
        : base(PageId.GpsCapture, "GPS Capture")
    {
        _gameDetectionService = gameDetectionService;
        _captureService = captureService;
        _profileStore = profileStore;
        _presetProvider = presetProvider;
        _navigationService = navigationService;
        _logger = logger;

        Games = new ObservableCollection<GpsGameChoiceViewModel>();
        CaptureSources = new ObservableCollection<CaptureSource>();
        RefreshGames(gameDetectionService.CurrentSnapshot);

        gameDetectionService.SnapshotChanged += OnGameDetectionChanged;
        captureService.FrameAvailable += OnFrameAvailable;
        captureService.SnapshotChanged += OnCaptureSnapshotChanged;
        profileStore.ProfilesChanged += (_, _) => Dispatch(() => RefreshGames(_gameDetectionService.CurrentSnapshot));
    }

    public ObservableCollection<GpsGameChoiceViewModel> Games { get; }
    public ObservableCollection<CaptureSource> CaptureSources { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStepOne))]
    [NotifyPropertyChangedFor(nameof(IsStepTwo))]
    [NotifyPropertyChangedFor(nameof(IsStepThree))]
    [NotifyPropertyChangedFor(nameof(IsStepFour))]
    [NotifyPropertyChangedFor(nameof(StepProgressText))]
    private int currentStep = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedGameName))]
    private GameType selectedGame = GameType.None;

    [ObservableProperty]
    private CaptureSource? selectedSource;

    [ObservableProperty]
    private ImageSource? fullPreview;

    [ObservableProperty]
    private ImageSource? cropPreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private PixelRectangle? selection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferenceResolution))]
    private int frameWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferenceResolution))]
    private int frameHeight;

    [ObservableProperty]
    private bool isCaptureAvailable;

    [ObservableProperty]
    private bool hasCaptureError;

    [ObservableProperty]
    private string captureStatusMessage = "Choose a running game to begin.";

    [ObservableProperty]
    private bool resolutionWarningVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DevicePreviewWidth))]
    [NotifyPropertyChangedFor(nameof(DevicePreviewHeight))]
    private DisplayOrientation previewOrientation = DisplayOrientation.Portrait;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewStretch))]
    [NotifyPropertyChangedFor(nameof(DisplayFitLabel))]
    private DisplayFitMode displayFit = DisplayFitMode.Fill;

    public bool IsStepOne => CurrentStep == 1;
    public bool IsStepTwo => CurrentStep == 2;
    public bool IsStepThree => CurrentStep == 3;
    public bool IsStepFour => CurrentStep == 4;
    public string StepProgressText => $"{CurrentStep} of 4  |  Choose Game  -  Select Area  -  Preview  -  Save";
    public string SelectedGameName => SelectedGame == GameType.None
        ? "No game selected"
        : SupportedGameCatalog.GetDisplayName(SelectedGame);
    public string ReferenceResolution => FrameWidth > 0 && FrameHeight > 0
        ? $"{FrameWidth} x {FrameHeight}"
        : "Waiting for frame";
    public double DevicePreviewWidth => PreviewOrientation == DisplayOrientation.Portrait ? 160 : 270;
    public double DevicePreviewHeight => PreviewOrientation == DisplayOrientation.Portrait ? 260 : 160;
    public Stretch PreviewStretch => DisplayFit == DisplayFitMode.Fill ? Stretch.UniformToFill : Stretch.Uniform;
    public string DisplayFitLabel => DisplayFit == DisplayFitMode.Fill ? "Fill" : "Fit";
    public bool HasSelection => Selection is not null;

    [RelayCommand]
    private async Task UseGameAsync(GameType gameType)
    {
        SelectedGame = gameType;
        _activeProfile = _profileStore.Get(gameType);
        _selectionInitialized = false;
        ResolutionWarningVisible = false;
        _resolutionChangeTracker.Reset();

        await RefreshSourcesAndStartAsync(preferAutomaticSource: true);
        CurrentStep = 2;
    }

    [RelayCommand]
    private async Task CheckAgainAsync()
    {
        var snapshot = await _gameDetectionService.RefreshAsync();
        RefreshGames(snapshot);
        if (SelectedGame != GameType.None && snapshot.GetGame(SelectedGame)?.WindowAvailable == true)
        {
            await RefreshSourcesAndStartAsync(preferAutomaticSource: true);
        }
    }

    [RelayCommand]
    private async Task ApplySourceAsync()
    {
        if (SelectedSource is not null)
        {
            await StartCaptureAsync(SelectedSource);
        }
    }

    [RelayCommand]
    private async Task TryAgainAsync()
    {
        if (SelectedSource?.SourceType == CaptureSourceType.Monitor)
        {
            await StartCaptureAsync(SelectedSource);
        }
        else
        {
            await _gameDetectionService.RefreshAsync();
            await RefreshSourcesAndStartAsync(preferAutomaticSource: true);
        }
    }

    [RelayCommand]
    private void ResetSelection()
    {
        if (FrameWidth <= 0 || FrameHeight <= 0)
        {
            return;
        }

        Selection = _activeProfile is not null
            ? CaptureRegionMath.Project(_activeProfile.Region, FrameWidth, FrameHeight)
            : CaptureRegionMath.CreateDefaultSelection(FrameWidth, FrameHeight);
    }

    [RelayCommand]
    private void FitStandardGps()
    {
        var preset = _presetProvider.GetStandardRouteAdvisorPreset(SelectedGame, FrameWidth, FrameHeight);
        if (preset is null)
        {
            CaptureStatusMessage = "No reliable Route Advisor preset is available for this source. Adjust the box manually.";
            return;
        }

        Selection = CaptureRegionMath.Project(preset, FrameWidth, FrameHeight);
        CaptureStatusMessage = "Standard Route Advisor preset applied. Adjust it if your game layout differs.";
    }

    [RelayCommand]
    private void PreviewSelection()
    {
        if (Selection is not null && IsCaptureAvailable)
        {
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void ContinueToSave()
    {
        if (Selection is not null)
        {
            CurrentStep = 4;
        }
    }

    [RelayCommand]
    private void SetOrientation(DisplayOrientation orientation) => PreviewOrientation = orientation;

    [RelayCommand]
    private void ToggleDisplayFit() =>
        DisplayFit = DisplayFit == DisplayFitMode.Fill ? DisplayFitMode.Fit : DisplayFitMode.Fill;

    [RelayCommand]
    private void KeepCurrentResolution()
    {
        _dismissedResolutionWidth = FrameWidth;
        _dismissedResolutionHeight = FrameHeight;
        ResolutionWarningVisible = false;
        _resolutionChangeTracker.Reset();
    }

    [RelayCommand]
    private void ReviewGpsArea()
    {
        ResolutionWarningVisible = false;
        CurrentStep = 2;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedGame == GameType.None || SelectedSource is null || Selection is null || FrameWidth <= 0 || FrameHeight <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var normalized = CaptureRegionMath.Normalize(
            Selection.X,
            Selection.Y,
            Selection.Width,
            Selection.Height,
            FrameWidth,
            FrameHeight);
        var existing = _profileStore.Get(SelectedGame);
        _profileStore.Save(new GpsCaptureProfile(
            GpsCaptureProfile.CurrentSchemaVersion,
            SelectedGame,
            SelectedSource.SourceType,
            SelectedSource.Id,
            normalized.X,
            normalized.Y,
            normalized.Width,
            normalized.Height,
            FrameWidth,
            FrameHeight,
            DisplayFit,
            existing?.CreatedAtUtc ?? now,
            now));
        _logger.LogInformation(
            "GPS capture profile saved for {GameType}: {SourceId}, crop {Crop}",
            SelectedGame,
            SelectedSource.Id,
            normalized);

        await _captureService.StopAsync();
        ResetWizard();
        _navigationService.NavigateTo(PageId.Home);
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            if (CurrentStep == 1)
            {
                await _captureService.StopAsync();
            }

            return;
        }

        await DeactivateAsync();
        _navigationService.NavigateTo(PageId.Home);
    }

    public async Task DeactivateAsync()
    {
        await _captureService.StopAsync();
        ResetWizard();
    }

    partial void OnSelectionChanged(PixelRectangle? value)
    {
        if (_lastRenderedFrame is not null && value is not null)
        {
            RenderCrop(_lastRenderedFrame, value);
        }
    }

    private async Task RefreshSourcesAndStartAsync(bool preferAutomaticSource)
    {
        CaptureSources.Clear();
        IReadOnlyList<CaptureSource> sources;
        try
        {
            sources = await _captureService.GetSourcesAsync();
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
            return;
        }

        foreach (var source in sources)
        {
            CaptureSources.Add(source);
        }

        var automatic = sources.FirstOrDefault(source =>
            source.SourceType == CaptureSourceType.AutoWindow && source.GameType == SelectedGame);
        SelectedSource = preferAutomaticSource
            ? automatic
            : SelectedSource ?? automatic;

        if (SelectedSource is null)
        {
            ShowCaptureError($"TruckNav can see {SelectedGameName} is running but can't show its window here.");
            return;
        }

        await StartCaptureAsync(SelectedSource);
    }

    private async Task StartCaptureAsync(CaptureSource source)
    {
        try
        {
            HasCaptureError = false;
            IsCaptureAvailable = false;
            CaptureStatusMessage = $"Starting {source.DisplayName}...";
            _selectionInitialized = false;
            _logger.LogInformation("Capture source selected: {SourceName}", source.DisplayName);
            await _captureService.StartAsync(source);
            CaptureStatusMessage = "Live game pixels. Drag and resize the amber box around the Route Advisor map.";
        }
        catch (Exception exception)
        {
            ShowCaptureError(exception.Message);
        }
    }

    private void OnFrameAvailable(object? sender, VideoFrame frame)
    {
        _pendingUiFrames.Publish(frame);
        if (Interlocked.CompareExchange(ref _uiFrameScheduled, 1, 0) != 0)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(RenderPendingFrame, DispatcherPriority.Render);
    }

    private void RenderPendingFrame()
    {
        var frame = _pendingUiFrames.Take();
        if (frame is not null)
        {
            RenderFrame(frame);
        }

        Interlocked.Exchange(ref _uiFrameScheduled, 0);
        if (_pendingUiFrames.HasValue &&
            Interlocked.CompareExchange(ref _uiFrameScheduled, 1, 0) == 0)
        {
            Application.Current.Dispatcher.BeginInvoke(RenderPendingFrame, DispatcherPriority.Render);
        }
    }

    private void RenderFrame(VideoFrame frame)
    {
        _lastRenderedFrame = frame;
        FrameWidth = frame.Width;
        FrameHeight = frame.Height;
        IsCaptureAvailable = true;
        HasCaptureError = false;

        if (_fullPreviewBitmap is null ||
            _fullPreviewBitmap.PixelWidth != frame.Width ||
            _fullPreviewBitmap.PixelHeight != frame.Height)
        {
            _fullPreviewBitmap = new WriteableBitmap(
                frame.Width,
                frame.Height,
                96,
                96,
                PixelFormats.Pbgra32,
                null);
            FullPreview = _fullPreviewBitmap;
        }

        _fullPreviewBitmap.WritePixels(
            new Int32Rect(0, 0, frame.Width, frame.Height),
            frame.Data,
            frame.Stride,
            0);

        if (!_selectionInitialized)
        {
            Selection = _activeProfile is not null
                ? CaptureRegionMath.Project(_activeProfile.Region, frame.Width, frame.Height)
                : CaptureRegionMath.CreateDefaultSelection(frame.Width, frame.Height);
            DisplayFit = _activeProfile?.DisplayFit ?? DisplayFitMode.Fill;
            _selectionInitialized = true;
        }

        if (Selection is not null)
        {
            Selection = CaptureRegionMath.Clamp(Selection, frame.Width, frame.Height);
            RenderCrop(frame, Selection);
        }

        ObserveResolution(frame);
    }

    private void RenderCrop(VideoFrame frame, PixelRectangle crop)
    {
        var clamped = CaptureRegionMath.Clamp(crop, frame.Width, frame.Height);
        var cropStride = clamped.Width * 4;
        var cropData = new byte[cropStride * clamped.Height];
        for (var row = 0; row < clamped.Height; row++)
        {
            Buffer.BlockCopy(
                frame.Data,
                (clamped.Y + row) * frame.Stride + clamped.X * 4,
                cropData,
                row * cropStride,
                cropStride);
        }

        if (_cropPreviewBitmap is null ||
            _cropPreviewBitmap.PixelWidth != clamped.Width ||
            _cropPreviewBitmap.PixelHeight != clamped.Height)
        {
            _cropPreviewBitmap = new WriteableBitmap(
                clamped.Width,
                clamped.Height,
                96,
                96,
                PixelFormats.Pbgra32,
                null);
            CropPreview = _cropPreviewBitmap;
        }

        _cropPreviewBitmap.WritePixels(
            new Int32Rect(0, 0, clamped.Width, clamped.Height),
            cropData,
            cropStride,
            0);
    }

    private void ObserveResolution(VideoFrame frame)
    {
        if (_activeProfile is null ||
            (_dismissedResolutionWidth == frame.Width && _dismissedResolutionHeight == frame.Height))
        {
            return;
        }

        if (_resolutionChangeTracker.Observe(
                _activeProfile.ReferenceSourceWidth,
                _activeProfile.ReferenceSourceHeight,
                frame.Width,
                frame.Height,
                frame.CapturedAtUtc))
        {
            ResolutionWarningVisible = true;
            _logger.LogWarning(
                "Capture resolution changed for {GameType}: {ReferenceWidth}x{ReferenceHeight} to {CurrentWidth}x{CurrentHeight}",
                SelectedGame,
                _activeProfile.ReferenceSourceWidth,
                _activeProfile.ReferenceSourceHeight,
                frame.Width,
                frame.Height);
        }
    }

    private void OnGameDetectionChanged(object? sender, GameDetectionSnapshot snapshot)
    {
        Dispatch(() =>
        {
            RefreshGames(snapshot);
            if (SelectedGame == GameType.None || snapshot.GetGame(SelectedGame)?.ProcessRunning == true)
            {
                return;
            }

            _ = _captureService.StopAsync();
            IsCaptureAvailable = false;
            HasCaptureError = true;
            CaptureStatusMessage = $"{SelectedGameName} is no longer running. Your unsaved selection is preserved.";
            _logger.LogInformation("Game closed during GPS calibration: {GameType}", SelectedGame);
        });
    }

    private void OnCaptureSnapshotChanged(object? sender, CaptureRuntimeSnapshot snapshot)
    {
        if (snapshot.ErrorCode == CaptureErrorCode.None)
        {
            return;
        }

        Dispatch(() => ShowCaptureError(
            snapshot.ErrorCode == CaptureErrorCode.SourceClosed
                ? "The game window closed or was recreated. Check the game, then try again."
                : $"Game detected, but the preview is unavailable. {snapshot.TechnicalDetail}"));
    }

    private void RefreshGames(GameDetectionSnapshot snapshot)
    {
        Games.Clear();
        foreach (var definition in SupportedGameCatalog.All)
        {
            var running = snapshot.GetGame(definition.GameType);
            var canCapture = running?.ProcessRunning == true && running.WindowAvailable;
            Games.Add(new GpsGameChoiceViewModel
            {
                GameType = definition.GameType,
                Name = definition.DisplayName,
                IsRunning = running?.ProcessRunning == true,
                IsWindowAvailable = canCapture,
                IsConfigured = _profileStore.Get(definition.GameType) is not null,
                UseCommand = new AsyncRelayCommand(
                    () => UseGameAsync(definition.GameType),
                    () => canCapture)
            });
        }
    }

    private void ShowCaptureError(string detail)
    {
        IsCaptureAvailable = false;
        HasCaptureError = true;
        CaptureStatusMessage = detail;
    }

    private void ResetWizard()
    {
        CurrentStep = 1;
        SelectedGame = GameType.None;
        SelectedSource = null;
        Selection = null;
        FullPreview = null;
        CropPreview = null;
        _fullPreviewBitmap = null;
        _cropPreviewBitmap = null;
        _lastRenderedFrame = null;
        _pendingUiFrames.Take();
        _selectionInitialized = false;
        IsCaptureAvailable = false;
        HasCaptureError = false;
        ResolutionWarningVisible = false;
        CaptureStatusMessage = "Choose a running game to begin.";
        RefreshGames(_gameDetectionService.CurrentSnapshot);
    }

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(action);
    }
}
