# Third-Party Navigation Review

Research was performed outside the TruckNav repository under `C:\Users\ADMIN\Desktop\trucknav\research`. TruckNav itself currently has no permissive repository license, so GPL source was not copied into it.

## TruckSim-GPS/trucksim-gps-plugin

- Inspected commit: `c5686cb41b36d0978dabcce24f730bbc1bf0b4bf`
- Tag/release context: no tag at that commit
- License: MIT, copyright 2014 Hans
- Purpose: authoritative plugin source, MMF name, revision 12 shared struct, and SCS telemetry fields
- Source copied into TruckNav: no
- Artifact reused: locally built Release x64 DLL
- DLL SHA-256: `4902D3F367F999CD4FC83502794DF651C6719BCC393875472F5259E52BDDBB52`
- Attribution: bundled beside the binary in `ThirdParty/TruckSimGpsTelemetry/LICENSE` and `PROVENANCE.txt`
- Distribution: MIT notice must remain with the binary

TruckNav's C# reader is an independent implementation against the plugin's published memory layout.

## TruckSim-GPS/trucksim-gps-server

- Inspected commit: `44179801323d6c4b933c2232f6baeec79a89a3d2`
- Inspected tag: `1.4.0`
- License: GPL-3.0
- Purpose: architecture and behavior research only
- Source copied: no
- Binary/artifact reused: no
- Distribution: not bundled; direct reuse would require a deliberate GPL compatibility decision

## truckermudgeon/maps

- Inspected commit: `d56d0e3fb319230e84284f3029f8bda2c4b572a2`
- Describe context: `clis/navigator/v0.2.0-38-gd56d0e3`
- License: GPL-3.0
- Purpose: external local parser/generator for installed ETS2/ATS base and official DLC files; output schema research
- Source copied: no
- Binary/artifact reused: no bundled artifact
- External use: cloned, built, and invoked outside TruckNav for local generation
- Distribution: tool source is not committed or shipped. Generated data remains local/uncommitted while redistribution implications are unresolved.
- Local dependency audit: `npm install` reported 50 advisories (2 low, 29 moderate, 14 high, 5 critical) in this external GPL development tool. It is not part of either shipped TruckNav application; dependency remediation remains an upstream/tooling concern before any distribution decision.

TruckNav's graph importer, binary cache, spatial grid, destination index, and A* implementation were authored independently.

## dariowouters/ts-map

- Repository: <https://github.com/dariowouters/ts-map>
- Inspected commit: `87835b4b742166cd7895b6c89f71810309d3e89b`
- Describe context: `v0.8-166-g87835b4`
- License: MIT
- Purpose: visual behavior research for curved roads, prefab map-point transforms, map-area draw order, ferry paths, city labels, tiled export, and mod-aware installed-game rendering
- Source copied: no source file was copied into TruckNav
- Implementation boundary: TruckNav uses independently authored affine transforms, cubic Hermite interpolation, visual feature models, binary cache, LOD tile index, and Flutter painter
- Binary/artifact reused: no

The repository confirmed that prefab-local map points and their neighbor graph are the visual source for junctions and roundabouts. Generic affine transformation and Hermite interpolation are mathematical techniques, not imported implementation code.

## truckermudgeon/truckermudgeon.github.io

- Repository: <https://github.com/truckermudgeon/truckermudgeon.github.io>
- Inspected commit: `bbfba0007bc6ddc8d76510cc7de17ec78743b319`
- License: no license file was present in the deployed repository at the inspected commit; the source `truckermudgeon/maps` project is GPL-3.0
- Purpose: deployed-map behavior and styling research only
- Source copied: no
- Binary/artifact reused: no

## Rares-Muntean/TruckNav-Sim

- Inspected commit: `c579946cc233b63a0df659534f19c809ce9964d7`
- Inspected tag: `v0.5.0`
- License: GPL-3.0
- Purpose: behavior research for coordinate handling, road-edge state, rerouting, and camera expectations
- Source copied: no
- Binary/artifact reused: no
- Distribution: not bundled

## Local Generation Record

Generated on August 23, 2026 from installed game files:

| Game | Version | Runtime nodes | Edge records | Destinations | Import time | 10-route average |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| ETS2 | 1.60.1.7 | 193,762 | 401,376 | 1,831 | 3.7 s first import | 56.1 ms |
| ATS | 1.60.1.8 | 185,526 | 395,884 | 2,096 | 4.2 s first import | 53.1 ms |

Both installations were detected in `D:\SteamLibrary`. The generated source and compact caches are under `%LOCALAPPDATA%\TruckNav\navigation` and are excluded from Git by location.

Visual map datasets generated on August 24, 2026 from the same installed parser output:

| Game | TruckNav visual build | Compact cache | Roads | Prefab paths | Map areas | Ferries | Cities | POIs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| ETS2 | 3.6 s | 15,038,498 bytes | 113,653 | 173,332 | 8,817 | 65 | 374 | 20,890 |
| ATS | 3.1 s | 11,713,998 bytes | 110,428 | 108,800 | 26,697 | 5 | 276 | 12,892 |

These TruckNav-owned compact caches live under `%LOCALAPPDATA%\TruckNav\map`, are generated locally, and are never committed or bundled.
