# TruckNav

The TruckNav is an original native truck GPS for both Euro Truck Simulator 2 (ETS2) and American Truck Simulator (ATS). The TruckNav uses a Windows application called "TruckNav Connector" that connects to live SCS telemetry and generates local navigation data using your own ETS2/ATS game maps. That local data is sent via LAN to the mobile device where it is rendered as a fully-dedicated, user-friendly truck GPS display.

TruckNav includes two main components:

- The TruckNav Connector: A Windows WPF application that identifies all existing ETS2/ATS games installed on your computer, installs the telemetry plugin into those games, creates local navigation data for use by the mobile device, determines the best routes based on the data created, and shares that information with the connected device via the LAN.
- TruckNav Mobile: An Android-based application written in Flutter. This application will automatically discover and connect with your TruckNav Connector, and then display real-time information about your truck's location, planned route, maneuvers needed to complete the route (e.g., left turns), remaining distance to reach your destination, estimated time of arrival at your destination, current truck speed, and maximum allowed truck speed along the planned route.

You do not need a login, a cloud service, a working internet connection, manually entering your router's LAN IP address, taking a screenshot of the display, or having Route Advisor visible while you navigate with TruckNav. As TruckNav computes its own best routes, it may plan different roads than Route Advisor does.

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
