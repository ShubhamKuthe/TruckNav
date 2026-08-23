# TruckNav

TruckNav is a two-app LAN-first system that mirrors the live ETS2/ATS Route Advisor navigation area from a Windows PC to an Android phone or tablet.

TruckNav consists of:

- `TruckNav Connector`: a Windows WPF application that detects ETS2/ATS, manages GPS capture profiles, hosts local discovery/signaling services, and streams the cropped navigation region.
- `TruckNav Mobile`: a Flutter Android application that discovers the connector automatically, connects on the same local network, displays the live GPS feed fullscreen, and sends semantic navigation commands back to the PC.

## What TruckNav Does Not Do

TruckNav does not contain its own truck map engine.
TruckNav does not calculate routes.
TruckNav does not require accounts, cloud services, manual IP entry, or internet access.

The product mirrors the navigation map rendered by ETS2/ATS. If the game is not rendering the Route Advisor/GPS region, there is nothing for V1 to capture.

## Current Status

This repository was bootstrapped from the master implementation specification on August 23, 2026.

Phase 1 is in progress:

- monorepo structure
- documentation skeleton
- Windows Connector solution and MVVM/theme shell
- Flutter mobile shell and shared state/navigation skeleton

The approved visual reference files expected by the specification are currently missing from the repository:

- `design-reference/trucknav-mobile.html`
- `design-reference/trucknav-connector.html`

UI work in this scaffold therefore focuses on reusable tokens, navigation structure, and explicit application state, without claiming final design fidelity until those files are added.

## Repository Layout

```text
design-reference/   Approved HTML design references (currently missing)
docs/               Architecture, protocol, build, test, security docs
protocol/           Protocol version file, schemas, examples
apps/windows/       Windows Connector solution and projects
apps/mobile/        Flutter Android application
scripts/            Build and test automation
artifacts/          Local publish/build output
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

See `docs/BUILDING.md` for toolchain details.

## Known Limitations

- The final approved Connector and Mobile layouts cannot be matched exactly until the missing design-reference HTML files are committed.
- Production WebRTC/H.264 transport is intentionally not faked in Phase 1. The repository isolates streaming behind interfaces so Phase 5/6 can be implemented without rewriting UI/domain code.
- The current mobile shell is Android-first for V1, but the Flutter architecture avoids unnecessary Android-only coupling.

