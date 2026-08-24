# TruckNav Architecture

## Product Boundary

TruckNav is an external native GPS for Euro Truck Simulator 2 and American Truck Simulator. It does not capture, crop, mirror, or decode the in-game Route Advisor. TruckNav calculates its own route to the active delivery destination, so its selected roads can differ from the game's route.

```text
ETS2 / ATS
    |
    v
SCS telemetry DLL (MIT)
    |
    v
Local\TSGPSTelemetry shared memory
    |
    v
TruckNav Connector
    +-- live truck and job state
    +-- installed-game routing graph (A* only)
    +-- installed-game visual dataset (rendering only)
    |
    +-- exact destination resolver + directed A*
    +-- compact vector feature cache + LOD tile index
    |
    v
existing ROCK <-> HEN WebSocket + authenticated map HTTP endpoints
    |
    v
Flutter local vector map + independent route/truck overlay
```

Everything remains local to the PC or LAN. There is no cloud account, analytics, external routing API, Google Maps, OpenStreetMap routing, or telemetry upload.

## Windows Projects

- `TruckNav.Connector.Core`: platform-independent navigation models and interfaces.
- `TruckNav.Connector.Navigation`: game detection, Steam discovery, plugin setup, shared-memory telemetry, graph import/cache, spatial index, destination lookup, A*, and route progress.
- `TruckNav.Connector.Networking`: mDNS, WebSocket session, protocol validation, and coalesced navigation broadcast.
- `TruckNav.Connector.App`: WPF setup, status, games, navigation, settings, and diagnostics UI.
- `TruckNav.Connector.Tests`: protocol, LAN, telemetry, routing, progress, identity, and game-detection tests.

The obsolete `TruckNav.Connector.Capture` project and all video/crop production code were removed after the native pipeline passed.

## Telemetry

The pinned TruckSim-GPS plugin writes revision 12 into `Local\TSGPSTelemetry` (32 KiB). TruckNav opens the existing map read-only and never creates a fake map. The reader validates revision and timestamps before publishing a snapshot.

Fields include SDK/game state, pause, game version, X/Y/Z, heading, speed, speed limit, active job, source/destination city and company IDs/names, game navigation distance/time, and game timestamp. Polling is 20 Hz on a hosted background service. Transitions are logged, not every coordinate.

## Installed Game And Plugin Setup

Steam libraries are discovered from the registry, `libraryfolders.vdf`, and `appmanifest_227300.acf` / `appmanifest_270880.acf`. Additional drives are supported. A validated manual folder fallback is stored at `%LOCALAPPDATA%\TruckNav\game-paths.json`.

The Games page installs only `trucksim-gps-telemetry.dll` into `<game>\bin\win_x64\plugins\`. Games must be closed. The bundled binary is built from pinned MIT source and includes its license, commit, and SHA-256 provenance.

## Navigation Data

For this local testing build, `truckermudgeon/maps` is a separate GPL-3.0 external tool outside TruckNav. Its source and generated map data are not committed or bundled. The Games page invokes it locally and then converts its actual JSON output into TruckNav's independent compact binary format.

Data lives under `%LOCALAPPDATA%\TruckNav\navigation\ets2` and `...\ats`. Metadata includes game/version, generator commit, format version, generation time, source path, and DLC signature. A game version or DLC signature mismatch invalidates the cache.

The runtime graph stores game-world X/Z coordinates, directed forward/backward adjacency, edge distance/duration, ferry flag, one-lane flag, DLC guard, and exact city/company destinations. Telemetry and graph use the same useful game-world plane, so `GameCoordinateTransform` is intentionally identity X/Z.

## Visual Map Data

The routing graph is never used as the Release base map. A separate TruckNav visual generator reads the parser's real roads, road looks, source nodes, prefab placements/descriptions/map points, map areas, dividers, ferries, cities, and POIs. Road node rotations produce curved cubic Hermite paths. Prefab-local neighbor geometry is transformed into world X/Z, preserving roundabout rings, junction exits, ramps, service areas, and company entrances.

The compact dataset is stored at `%LOCALAPPDATA%\TruckNav\map\ets2\trucknav-visual-map-v1.bin` or the ATS equivalent. Metadata contains game type/version, `trucknav-vector-1`, generation time, DLC signature, world bounds, and LOD definitions. Version/DLC mismatch marks it stale. Read-only tiles are generated and cached on demand under `tiles-v1`; the phone never receives whole-map JSON.

The three custom local vector levels use 32,768 m overview cells, 8,192 m normal cells, and 2,048 m close cells. Overview includes major roads, ferries, and priority cities. Normal adds junctions/prefabs and navigation POIs. Close adds minor roads, map polygons, dividers, companies, and detailed POIs.

## Routing And Progress

- A 750-meter grid spatial index gives bounded nearest-node searches and is cached per graph.
- Destination lookup uses exact normalized city/company tokens first and an unambiguous exact display-name fallback. There is no fuzzy guess.
- Directed A* minimizes generated edge duration with a straight-line `/ 60 m/s` admissible heuristic and a start-heading penalty.
- Ferry/train edges are retained and surfaced as maneuvers where present.
- Route progress projects the truck onto a local forward segment window with a global recovery fallback.
- Off-route threshold is 80 m. Rerouting requires three samples and at least one second of stable deviation, with a 10-second cooldown.
- A position jump of 2 km is treated as teleport/quick travel and triggers a current-position route rebuild.

Full route geometry is sent only when `RouteRevision` changes. Telemetry and progress are coalesced at up to 20 Hz, preventing an unbounded queue.

## Flutter GPS

The mobile GPS uses TruckNav's custom local vector tile renderer, not an online map SDK or video surface. Static tile payloads use bounded memory and Android disk caches. A separate painter renders the active route, destination, real truck pose, next maneuver, distance, ETA, speed, and speed limit above the base map.

The camera supports heading-up follow, world-up, fitted route overview, pinch zoom, temporary drag pan, and Recenter. A 90 ms animation interpolates between real telemetry samples using shortest-angle heading wrap. Jumps at or above 2 km snap immediately rather than animating across a teleport. Portrait and landscape follow the device layout, and the existing tap/three-second control behavior is preserved.

When visual data is absent or stale, Release displays `Map data unavailable` and asks for a ROCK map build. It never substitutes routing-graph lines, sample geometry, or simulated movement.
