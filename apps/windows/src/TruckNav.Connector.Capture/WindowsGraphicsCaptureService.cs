using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace TruckNav.Connector.Capture;

public sealed class WindowsGraphicsCaptureService : IGameCaptureService, IAsyncDisposable
{
    private static readonly TimeSpan PreviewFrameInterval = TimeSpan.FromMilliseconds(50);

    private readonly IGameDetectionService _gameDetectionService;
    private readonly IWindowsMonitorProvider _monitorProvider;
    private readonly ILogger<WindowsGraphicsCaptureService> _logger;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _snapshotGate = new();
    private readonly Stopwatch _captureClock = new();

    private IDirect3DDevice? _direct3DDevice;
    private GraphicsCaptureItem? _captureItem;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _captureSession;
    private CancellationTokenSource? _captureCancellation;
    private long _sequenceNumber;
    private long _lastAcceptedFrameTicks;
    private int _processingFrame;
    private int _framesSinceFpsUpdate;
    private long _fpsWindowStartTicks;

    public WindowsGraphicsCaptureService(
        IGameDetectionService gameDetectionService,
        IWindowsMonitorProvider monitorProvider,
        ILogger<WindowsGraphicsCaptureService> logger)
    {
        _gameDetectionService = gameDetectionService;
        _monitorProvider = monitorProvider;
        _logger = logger;
    }

    public CaptureRuntimeSnapshot CurrentSnapshot { get; private set; } = CaptureRuntimeSnapshot.Stopped;

    public event EventHandler<VideoFrame>? FrameAvailable;
    public event EventHandler<CaptureRuntimeSnapshot>? SnapshotChanged;

    public Task<IReadOnlyList<CaptureSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sources = new List<CaptureSource>();

        foreach (var game in _gameDetectionService.CurrentSnapshot.RunningGames
                     .Where(game => game.ProcessRunning && game.WindowAvailable && game.WindowHandle != 0)
                     .OrderBy(game => game.GameType))
        {
            try
            {
                var item = GraphicsCaptureInterop.CreateForWindow(game.WindowHandle);
                sources.Add(new CaptureSource(
                    $"game:{game.GameType}",
                    $"{Core.Utilities.SupportedGameCatalog.GetDisplayName(game.GameType)} - Auto",
                    CaptureSourceType.AutoWindow,
                    item.Size.Width,
                    item.Size.Height,
                    game.WindowHandle,
                    game.GameType));
            }
            catch (Exception exception) when (exception is COMException or InvalidOperationException)
            {
                _logger.LogWarning(
                    exception,
                    "Game window source unavailable for {GameType} (PID {ProcessId})",
                    game.GameType,
                    game.ProcessId);
            }
        }

        sources.AddRange(_monitorProvider.GetMonitorSources());
        return Task.FromResult<IReadOnlyList<CaptureSource>>(sources);
    }

    public async Task StartAsync(CaptureSource source, CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            StopCaptureCore(publishStoppedState: false);

            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new PlatformNotSupportedException("Windows Graphics Capture is unavailable on this Windows version.");
            }

            if (source.NativeHandle == 0 || source.SourceType == CaptureSourceType.DebugMock)
            {
                throw new ArgumentException("The production capture source has no native handle.", nameof(source));
            }

            _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _captureItem = source.SourceType == CaptureSourceType.Monitor
                ? GraphicsCaptureInterop.CreateForMonitor(source.NativeHandle)
                : GraphicsCaptureInterop.CreateForWindow(source.NativeHandle);
            _captureItem.Closed += OnCaptureItemClosed;

            _direct3DDevice = Direct3D11DeviceFactory.Create();
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _direct3DDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureItem.Size);
            _framePool.FrameArrived += OnFrameArrived;
            _captureSession = _framePool.CreateCaptureSession(_captureItem);

            try
            {
                _captureSession.IsCursorCaptureEnabled = false;
            }
            catch (Exception exception) when (exception is COMException or NotSupportedException)
            {
                _logger.LogDebug(exception, "Cursor capture could not be disabled");
            }

            _sequenceNumber = 0;
            _lastAcceptedFrameTicks = 0;
            _processingFrame = 0;
            _framesSinceFpsUpdate = 0;
            _fpsWindowStartTicks = Stopwatch.GetTimestamp();
            _captureClock.Restart();
            _captureSession.StartCapture();
            PublishSnapshot(new CaptureRuntimeSnapshot(
                true,
                source,
                _captureItem.Size.Width,
                _captureItem.Size.Height,
                0,
                CaptureErrorCode.None,
                null));

            _logger.LogInformation(
                "Capture started: {SourceName}, {Width}x{Height}",
                source.DisplayName,
                _captureItem.Size.Width,
                _captureItem.Size.Height);
        }
        catch (Exception exception)
        {
            StopCaptureCore(publishStoppedState: false);
            var errorCode = MapError(exception, source);
            PublishSnapshot(new CaptureRuntimeSnapshot(
                false,
                source,
                0,
                0,
                0,
                errorCode,
                exception.Message));
            _logger.LogError(exception, "Capture initialization failed for {SourceName}", source.DisplayName);
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            StopCaptureCore(publishStoppedState: true);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleGate.Dispose();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        Direct3D11CaptureFrame? frame = null;
        try
        {
            frame = sender.TryGetNextFrame();
            if (frame is null || _captureCancellation?.IsCancellationRequested != false)
            {
                frame?.Dispose();
                return;
            }

            var nowTicks = Stopwatch.GetTimestamp();
            var elapsedSinceLastFrame = Stopwatch.GetElapsedTime(_lastAcceptedFrameTicks, nowTicks);
            if (_lastAcceptedFrameTicks != 0 && elapsedSinceLastFrame < PreviewFrameInterval)
            {
                frame.Dispose();
                return;
            }

            if (Interlocked.CompareExchange(ref _processingFrame, 1, 0) != 0)
            {
                frame.Dispose();
                return;
            }

            _lastAcceptedFrameTicks = nowTicks;
            _ = ProcessFrameAsync(sender, frame, _captureCancellation.Token);
        }
        catch (Exception exception)
        {
            frame?.Dispose();
            Interlocked.Exchange(ref _processingFrame, 0);
            PublishFailure(CaptureErrorCode.CaptureSourceUnavailable, exception);
        }
    }

    private async Task ProcessFrameAsync(
        Direct3D11CaptureFramePool framePool,
        Direct3D11CaptureFrame captureFrame,
        CancellationToken cancellationToken)
    {
        try
        {
            var contentSize = captureFrame.ContentSize;
            var frame = await SoftwareBitmapFrameConverter.ConvertAsync(
                captureFrame,
                Interlocked.Increment(ref _sequenceNumber),
                cancellationToken);
            FrameAvailable?.Invoke(this, frame);
            UpdateFrameStatistics(frame.Width, frame.Height);

            if (contentSize.Width > 0 &&
                contentSize.Height > 0 &&
                (contentSize.Width != CurrentSnapshot.FrameWidth || contentSize.Height != CurrentSnapshot.FrameHeight))
            {
                framePool.Recreate(
                    _direct3DDevice!,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    contentSize);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            PublishFailure(CaptureErrorCode.CaptureSourceUnavailable, exception);
        }
        finally
        {
            captureFrame.Dispose();
            Interlocked.Exchange(ref _processingFrame, 0);
        }
    }

    private void UpdateFrameStatistics(int width, int height)
    {
        _framesSinceFpsUpdate++;
        var nowTicks = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_fpsWindowStartTicks, nowTicks);
        if (elapsed < TimeSpan.FromSeconds(1))
        {
            return;
        }

        var fps = _framesSinceFpsUpdate / elapsed.TotalSeconds;
        _framesSinceFpsUpdate = 0;
        _fpsWindowStartTicks = nowTicks;
        PublishSnapshot(CurrentSnapshot with
        {
            FrameWidth = width,
            FrameHeight = height,
            PreviewFramesPerSecond = Math.Round(fps, 1)
        });
    }

    private async void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
    {
        try
        {
            await _lifecycleGate.WaitAsync();
            try
            {
                if (!ReferenceEquals(sender, _captureItem))
                {
                    return;
                }

                PublishFailure(
                    CaptureErrorCode.SourceClosed,
                    new InvalidOperationException("The selected capture source was closed."));
                StopCaptureCore(publishStoppedState: false);
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void PublishFailure(CaptureErrorCode errorCode, Exception exception)
    {
        var source = CurrentSnapshot.Source;
        PublishSnapshot(CurrentSnapshot with
        {
            IsRunning = false,
            ErrorCode = errorCode,
            TechnicalDetail = exception.Message,
            PreviewFramesPerSecond = 0
        });
        _logger.LogWarning(exception, "Capture failure for {SourceName}", source?.DisplayName ?? "unknown source");
    }

    private void StopCaptureCore(bool publishStoppedState)
    {
        var sourceName = CurrentSnapshot.Source?.DisplayName;
        _captureCancellation?.Cancel();
        _captureCancellation?.Dispose();
        _captureCancellation = null;

        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
        }

        if (_captureItem is not null)
        {
            _captureItem.Closed -= OnCaptureItemClosed;
        }

        _captureSession?.Dispose();
        _framePool?.Dispose();
        _direct3DDevice?.Dispose();
        _captureSession = null;
        _framePool = null;
        _captureItem = null;
        _direct3DDevice = null;
        _captureClock.Reset();

        if (publishStoppedState)
        {
            PublishSnapshot(CaptureRuntimeSnapshot.Stopped);
        }

        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            _logger.LogInformation("Capture stopped: {SourceName}", sourceName);
        }
    }

    private void PublishSnapshot(CaptureRuntimeSnapshot snapshot)
    {
        lock (_snapshotGate)
        {
            CurrentSnapshot = snapshot;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    private static CaptureErrorCode MapError(Exception exception, CaptureSource source)
    {
        if (exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005))
        {
            return CaptureErrorCode.CaptureAccessDenied;
        }

        if (source.SourceType == CaptureSourceType.AutoWindow && source.NativeHandle == 0)
        {
            return CaptureErrorCode.GameWindowUnavailable;
        }

        if (exception is ArgumentException)
        {
            return CaptureErrorCode.CaptureSourceUnavailable;
        }

        return CaptureErrorCode.CaptureInitializationFailed;
    }
}
