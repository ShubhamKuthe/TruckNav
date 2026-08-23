# Troubleshooting

## Windows Build

- Install the .NET 8 SDK and Windows 10 SDK 19041 or newer.
- Run commands from the repository root.
- If WPF/WinRT references fail to restore, run `dotnet restore apps/windows/TruckNav.Connector.sln` and rebuild.

## Game Running But Window Unavailable

- Wait until the simulator has created its main game window; process detection can occur earlier.
- Return to GPS Capture and choose Check Again.
- If the game changed rendering mode or restarted, its HWND may have changed. Retry after Phase 3 reports the new window.
- TruckNav does not persist an HWND in the GPS profile.

## Blank Or Unavailable Preview

- Restore the game if it is minimized. Windows Graphics Capture does not reliably capture minimized windows.
- Try windowed or borderless mode. Exclusive fullscreen support varies by Windows, renderer, GPU driver, and protected-content behavior.
- Click Try Again after the game is visible.
- If direct window capture remains unavailable, explicitly choose the correct monitor as a fallback. Monitor capture can include anything visible on that display, so use it only when needed.

## Multiple Monitors

- Automatic game-window capture is preferred and does not require TruckNav to be on the same monitor.
- Monitor fallback dimensions are physical pixels. The app is Per-Monitor-V2 DPI aware.
- If a monitor is added, removed, rotated, or has its scale changed, reopen calibration and refresh the source list.

## Resolution Changed

- TruckNav warns only after a greater-than-five-percent size change remains stable for at least 750 ms across three observations.
- Keep Current retains the normalized crop and applies it to the new dimensions.
- Review GPS Area opens the live selection with the old normalized crop projected into the new frame.
- One-pixel changes and brief launch/alt-tab transitions should not trigger a warning.

## Windows DPI Scaling

- The selection is stored in captured-frame pixels and normalized values, not WPF DIPs.
- At 125%, 150%, 175%, or 200% scaling, the overlay maps through the actual uniformly rendered image bounds.
- If changing scale while calibration is open produces stale layout, close and reopen GPS Capture so Windows and WPF refresh the monitor transform.

## Game Closed During Calibration

- TruckNav stops capture and reports that the selected game is no longer running.
- The current unsaved rectangle remains available in memory while the wizard is open.
- Restart the game, choose Check Again, and retry capture.

## Flutter And LAN

- Phase 4 does not transmit video to Android. The mobile GPS surface should continue showing its honest waiting state.
- For Android build problems, confirm `flutter --version`, run `flutter doctor`, and use Java 17 or newer for release packaging.
- For discovery/connectivity issues, ensure both devices are on the same LAN and TruckNav Connector is allowed on the Windows Private network profile.
