# Testing

## Automated Windows Coverage

Run from the repository root:

```powershell
dotnet build apps/windows/TruckNav.Connector.sln
dotnet test apps/windows/TruckNav.Connector.sln
pwsh ./scripts/build_windows.ps1
```

Phase 4 automated coverage includes:

- crop normalization, projection, clamping, minimum size, and invalid-value rejection
- selection movement/resizing within frame bounds
- 100%, 125%, and 150% DPI conversion
- WPF letterbox-to-frame coordinate mapping
- material resolution-change threshold and stable-observation debounce
- normalized crop preservation across source resolutions
- independent ETS2/ATS profile persistence
- malformed profile recovery and invalid profile rejection
- known 16:9 Route Advisor preset and no-preset behavior
- mock capture start/frame/stop lifecycle, game-close stop, and source replacement
- latest-value frame buffering with a hard capacity of one

Existing protocol, LAN integration, session, identity, mDNS, and Phase 3 game-detection tests remain enabled.

## Flutter Regression Coverage

From `apps/mobile` run:

```powershell
flutter analyze
flutter test
pwsh ../../scripts/build_android.ps1
```

Phase 4 changes no Android video behavior. The existing Android waiting/placeholder surface and Phase 3 `game_state` synchronization must continue to pass.

## Real ETS2/ATS Acceptance

Automated tests use fake frames and do not prove that a particular simulator/GPU/display-mode combination can be captured. Verify the real flow manually:

1. Start TruckNav Connector and ETS2 or ATS.
2. Confirm the Games page reports the simulator and its window as running.
3. Open GPS Capture, choose the running game, and click Use This Game.
4. Confirm the preview shows the actual simulator window, not generated artwork.
5. Open the Route Advisor and drag/resize the amber box around only its map portion.
6. Confirm all handles remain aligned at the current Windows scaling setting.
7. Open Preview and confirm the crop contains exactly those real pixels.
8. Switch Portrait/Landscape and Fill/Fit; confirm there is no stretching.
9. Save and confirm Home and Games report that game as configured.
10. Restart TruckNav Connector and the simulator; confirm the separate profile persists.
11. Change the game resolution materially and confirm the stable warning offers Keep Current and Review GPS Area.
12. Close the game during calibration and confirm the app preserves the unsaved rectangle and does not crash.

## Multi-Monitor, Alt-Tab, And Performance

When a second display is available, place TruckNav and the game on different monitors, preferably with different scale factors. Confirm automatic window capture is independent of the WPF monitor. If automatic capture is unavailable, explicitly select the intended monitor and verify the crop still maps to physical capture pixels.

While preview is open, cover and alt-tab away from the game, then return. A minimized source may stop delivering useful frames; this is a Windows Graphics Capture limitation. Confirm retry recovers after the game window returns.

Leave calibration open for several minutes and observe CPU, GPU, and memory. Preview should remain near 20 FPS, memory should stabilize, and there should be no growing delay because both native and WPF queues are bounded/latest-only.
