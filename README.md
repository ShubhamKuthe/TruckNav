# TruckNav

TruckNav is a LAN-first native truck GPS for Euro Truck Simulator 2 and American Truck Simulator. A Windows connector reads live SCS telemetry and calculates a route from locally generated game-map data; the Android app renders that route as a dedicated navigation display.

TruckNav consists of:

- `TruckNav Connector`: a Windows WPF application that detects ETS2/ATS installations, installs the telemetry plugin, builds local navigation data, calculates routes, and exposes them to paired devices over the LAN.
- `TruckNav Mobile`: a Flutter Android application that discovers and pairs with the connector, then renders the truck position, route, maneuver, distance, ETA, speed, and speed limit.

No account, cloud service, internet connection, manual IP entry, screen capture, or Route Advisor visibility is required during navigation. TruckNav calculates its own fastest route, so its selected roads can differ from the in-game advisor.

## Repository Layout

```text
design-reference/   Approved HTML design references
docs/               Architecture, protocol, build, test, and security docs
protocol/           Protocol version, schemas, and examples
apps/windows/       Windows Connector solution
apps/mobile/        Flutter Android application
scripts/            Build and test automation
artifacts/          Local publish output
```

## Build

Windows:

```powershell
pwsh ./scripts/build_windows.ps1
pwsh ./scripts/test_windows.ps1
```

Android / Flutter:

```powershell
pwsh ./scripts/build_android.ps1
pwsh ./scripts/test_android.ps1
```

See `docs/BUILDING.md` for toolchain details and `docs/THIRD_PARTY_NAVIGATION_REVIEW.md` for pinned-source and license provenance.

## Current Limitations

- Local graph generation currently depends on a separately cloned and built `truckermudgeon/maps` tool; its GPL source and binaries are not distributed in this repository.
- Installed third-party map mods are not supported by the validated graph-generation workflow.
- Native route behavior has automated and offline installed-map coverage, but a live end-to-end drive with Route Advisor disabled still requires manual validation.
