# TruckNav Protocol

## Version And Discovery

Protocol version 2 adds native navigation. App version remains independent at `0.1.0`. Windows advertises `_trucknav._tcp.local` with device identity, version, role, and capabilities. Current connector capabilities are `native_navigation`, `scs_telemetry`, and `local_route_graph`.

The existing `hello` / `hello_ack`, typed errors, one-active-session rule, `ping` / `pong`, reconnect, `game_state`, and `game_state_request` behavior is preserved. Version 1 peers are intentionally rejected because they cannot parse native navigation state.

Each successful `hello_ack` includes a fresh 256-bit `mapAccessToken`. It is valid only while that exact WebSocket session is active and is sent as `X-TruckNav-Map-Token` on map requests.

## Initial Sync

After an accepted hello, ROCK sends:

1. `hello_ack`
2. current `game_state`
3. current `telemetry_state`
4. current `route_state`, including an unavailable state when no route exists

This makes reconnect restore the latest route without waiting for the next job transition.

## Navigation Messages

### telemetry_state

Sent on useful telemetry/status updates, coalesced to avoid backlog. Contains game, connection/pause/job state, X/Z/elevation, heading, speed, speed limit, destination labels, game navigation distance/time, navigation status/message, and timestamp.

### route_state

Sent only when `routeRevision` changes or during initial sync. Contains availability, route ID, game, exact resolved destination, route points, maneuver steps, bounded nearby road geometry (`contextRoads`), verified map symbols (`mapPoints`), city labels (`mapLabels`), total distance/time, calculation timestamp, and reroute flag. A route removal is an explicit `available: false` message.

Context-road entries carry endpoints plus major-road and ferry flags. Map points carry one of `Fuel`, `Rest`, `Service`, `Company`, `Garage`, `Dealer`, `Ferry`, or `WeighStation`; these values come from generated parser data or exact company destinations, never fabricated mobile-side POIs.

### route_progress

Sent with live progress while a route exists. Contains route ID, segment index, projected position, distance from route, remaining distance/time, next maneuver/instruction, distance to maneuver, off-route/teleport flags, and timestamp.

The full installed graph and full visual dataset are never transmitted. A normal telemetry tick never resends route geometry or map tiles.

## Local Visual Map Endpoints

- `GET /map/{game}/manifest` returns game/version/generator/DLC metadata, world bounds, and LOD cell sizes.
- `GET /map/{game}/{level}/{tileX}/{tileY}` returns a gzip-compressed read-only TruckNav vector tile.
- Both endpoints require the active session's map token; unauthenticated, expired, or previous-session tokens return `401`.
- Tiles contain roads, prefab roads, map polygons, ferry/train links, city labels, and verified POIs in the same game-world X/Z coordinate system as route and telemetry.
- Responses are cacheable privately. There is no path parameter that maps directly to an arbitrary filesystem path.

## Validation And Transport

Client messages remain typed and size-limited to 16 KiB. The only post-handshake client messages are `ping` and `game_state_request`; unsupported input receives a typed error and closes the session. Server route messages can be larger than client control input because they carry the current polyline.

The current transport is cleartext `ws://` plus token-gated `http://` on the local LAN. It has no arbitrary file, shell, keyboard, or filesystem endpoint.
