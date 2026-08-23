# TruckNav Architecture

## Product Boundary

TruckNav mirrors the real ETS2/ATS Route Advisor navigation surface. It does not ship a road graph, routing engine, cloud backend, generated map, or fake production map rendering.

Phase 4 ends at local Windows capture and crop calibration. Captured pixels are not sent to Android yet, and a configured GPS area must not be described as streaming.

## Current System Shape

```text
WindowsGameDetectionService (Phase 3)
        |
        v
RunningGameInfo: GameType / PID / HWND / window state
        |
        v
WindowsGraphicsCaptureService
        |
        +-- automatic game-window source
        +-- explicit monitor fallback
        |
        v
Windows.Graphics.Capture frame pool (2 buffers)
        |
        v
VideoFrame (owned BGRA8 managed preview snapshot)
        |
        +-- reusable WPF selection overlay
        +-- real crop preview
        |
        v
normalized GpsCaptureProfile per game
```

The Phase 2 LAN discovery, protocol, and session services continue independently. Phase 3 game state still synchronizes to Android. Phase 4 adds no video protocol messages.

## Capture Boundary

`IGameCaptureService` remains the boundary consumed by the WPF calibration ViewModel. Production dependency injection registers `WindowsGraphicsCaptureService`; `MockGameCaptureService` is development/test only.

Native and WinRT interop stays inside `TruckNav.Connector.Capture`:

- `IGraphicsCaptureItemInterop.CreateForWindow` creates a `GraphicsCaptureItem` from the HWND supplied by Phase 3.
- `CreateForMonitor` is used only after the user explicitly chooses a display fallback.
- `D3D11CreateDevice` creates a BGRA-capable Direct3D 11 device, with WARP fallback if hardware creation fails.
- `CreateDirect3D11DeviceFromDXGIDevice` exposes that device to Windows Graphics Capture.
- `Direct3D11CaptureFramePool.CreateFreeThreaded` supplies frame-driven callbacks without a WPF dispatcher dependency.

The frame pool contains two buffers. Incoming frames are discarded while one GPU-to-CPU preview conversion is active. Capture is throttled to about 20 FPS, and the WPF side retains one latest pending frame. There is no unbounded queue and stale previews are preferred for dropping.

`SoftwareBitmap.CreateCopyFromSurfaceAsync` performs the Phase 4 WPF preview copy. Row pitch is read from `BitmapBuffer`, and each emitted `VideoFrame` owns a tightly packed BGRA8 byte array. Native frame/surface lifetime ends inside the capture provider. A later streaming phase can consume native surfaces directly without changing WPF or protocol code.

## Capture Sources

Game capture sources have stable logical IDs such as `game:EuroTruckSimulator2`; saved profiles do not persist a transient HWND. The current HWND is always obtained from `IGameDetectionService`.

Monitor enumeration is behind `IWindowsMonitorProvider`. Win32 monitor bounds are physical pixels because the WPF executable declares Per-Monitor-V2 DPI awareness. The definitive source size still comes from `GraphicsCaptureItem.Size` and captured frames.

## Coordinate Systems

TruckNav keeps these spaces separate:

- WPF input and layout use device-independent pixels (DIPs).
- monitor bounds and Windows capture frames use physical pixels.
- the selection overlay maps its uniformly fitted WPF image rectangle into captured-frame pixels.
- saved crop values are normalized fractions from 0.0 to 1.0.

No calculation assumes one WPF DIP equals one captured pixel. `CaptureCoordinateMapper` contains explicit DIP/physical conversion helpers, and `CaptureSelectionMath` clamps all move/resize operations to the captured frame with a 64 x 64 pixel minimum.

## Profiles

`JsonGpsCaptureProfileStore` writes `%LOCALAPPDATA%\TruckNav\gps-capture-profiles.json`. The document stores one independent profile per `GameType` with:

- schema version
- logical source type and ID
- normalized X/Y/width/height
- reference source width/height
- Fill/Fit preference
- UTC creation/update timestamps

Writes use a same-directory temporary file followed by replacement. Invalid records and malformed JSON are rejected without crashing startup. ETS2 profiles are never reused for ATS.

## Resolution Changes

A source-size change is material when either dimension differs by more than five percent. It must be observed three times over at least 750 ms before the UI warns, which filters launch, minimize, alt-tab, and rendering-mode transients. Keeping the current profile retains its normalized crop. Reviewing projects the same normalized crop into the current frame.

## Capture Lifecycle And Limitations

Capture starts only after a running game is selected for calibration. It stops when the user returns to step 1, leaves the page, saves, the game/source closes, or the app exits.

Windows Graphics Capture normally captures a window while it is covered by another window, but minimized windows are not reliably captured. Borderless and windowed modes are expected to work. Exclusive fullscreen behavior depends on Windows, the game renderer, GPU driver, and protected-content rules, so TruckNav does not promise every fullscreen configuration. A recreated game HWND requires retrying against the refreshed Phase 3 window state.

## Direct Dependencies

Windows Graphics Capture uses the Windows SDK projection selected by `net8.0-windows10.0.19041.0`; no third-party screen-capture package was added.

Existing direct Windows dependencies remain:

- `CommunityToolkit.Mvvm` `8.4.2`
- `Microsoft.Extensions.Hosting` `8.0.1`
- `Makaretu.Dns.Multicast.New` `0.38.0`

Existing Flutter dependencies remain unchanged by Phase 4.
