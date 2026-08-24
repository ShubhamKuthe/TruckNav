# Testing TruckNav

## Automated Validation

From the repository root:

```powershell
dotnet build apps/windows/TruckNav.Connector.sln
dotnet test apps/windows/TruckNav.Connector.sln
pwsh ./scripts/build_windows.ps1

cd apps/mobile
flutter analyze
flutter test
cd ../..
pwsh ./scripts/build_android.ps1
```

Windows coverage includes protocol/session/mDNS/game detection, map-token authorization, revision-12 MMF decoding, coordinates, heading, speed, limit, job/destination values, pickup-to-delivery target transitions, revision rejection, strict destination lookup, directed A*, no-route behavior, progress projection, and stable off-route rerouting. Flutter coverage includes native messages, pairing/session regressions, GPS controls, interpolation, visual tile decoding, a four-exit roundabout, divided interchange/ramp geometry, city/POI LOD data, route/map coordinate alignment, and pickup/delivery endpoint styling.

Automated tests do not require a running simulator and do not prove a real SCS session.

## First Real Setup

1. Start TruckNav Connector.
2. Open Games & Map Data.
3. Confirm ETS2/ATS installation path and version. Use Choose Folder only if Steam discovery missed it.
4. Click Install / Update Plugin. Close the game first.
5. Build the Navigation Graph if it is missing/stale.
6. On Navigation, click Build Map for the separate Visual Map if it is missing/stale.
7. Start the game and accept the SCS SDK warning if shown.
8. Restart the game once after first plugin installation.
9. Confirm Navigation shows telemetry connected.
10. Select a Freight Market job and confirm the source depot pickup route appears before attaching the trailer.
11. Attach the trailer and confirm TruckNav automatically switches to the delivery destination.
12. Pair HEN and open GPS.

## Mandatory Route Advisor Off Test

1. Start Connector and ETS2/ATS.
2. Confirm telemetry connected and take a delivery.
3. Confirm the current destination appears on ROCK and the route appears on HEN.
4. Hide/disable the in-game Route Advisor using the normal game control.
5. Drive.

Expected: the route stays visible; the truck marker continues moving/rotating; speed, progress, and rerouting continue. TruckNav must not depend on Route Advisor pixels.

## Real Driving Checklist

- **A - Straight movement:** marker advances smoothly.
- **B - Turning:** marker/camera rotates without long-angle wrap.
- **C - Follow TruckNav:** remaining progress moves forward.
- **D - Miss a turn:** after stable deviation, HEN shows rerouting and receives a route from the current position.
- **E - Follow a different game route:** TruckNav recalculates rather than forcing a missed edge forever.
- **F - Stop:** marker stops; no simulated drift.
- **G - Pause:** pose remains stable and route is not corrupted.
- **H - Close game:** telemetry disconnects gracefully while ROCK/HEN remains paired.
- **I - Restart game:** telemetry and active-job routing recover without restarting Connector.
- **J - Route Advisor off:** navigation works normally.

Record the game/version, source/destination, route calculation result, coordinate alignment, reroute timing, and any road-direction error. Do not report a manual acceptance item as passed unless it was observed in the game.

## Local Data Validation On This Machine

On August 24, 2026, production import, route planning, visual-map generation, and real gzip tile creation were exercised against installed ETS2 1.60.1.7 and ATS 1.60.1.8 data. Visual caches built in 3.6 s (ETS2, 15,038,498 bytes) and 3.1 s (ATS, 11,713,998 bytes). Release validation passed 65 Windows tests, 59 Flutter tests, and clean Flutter analysis. A real in-game roundabout/interchange appearance still requires driving those locations.
