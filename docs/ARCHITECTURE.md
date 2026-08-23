# TruckNav Architecture

## Product Boundary

TruckNav mirrors the ETS2/ATS navigation surface.

It still does not ship:

- a separate road graph
- a routing engine
- a cloud backend
- fake production map rendering

## Phase 2 System Shape

```text
Windows WPF Shell
   │
   ▼
Connector Session Store
   │
   ├── Kestrel LAN service
   │      ├── GET /health
   │      ├── GET /info
   │      └── WebSocket /ws
   │
   └── mDNS advertisement (_trucknav._tcp.local)

Android Flutter Shell
   │
   ▼
SessionController
   │
   ├── SharedPreferences identity/settings
   ├── multicast_dns discovery
   └── WebSocket control client + ping/pong + reconnect
```

Phase 2 stops at LAN discovery plus typed signaling.

It does not yet implement:

- ETS2/ATS process detection
- Windows capture
- video streaming
- WebRTC
- game input injection

## Service Boundaries

### Windows Connector

- `TruckNav.Connector.App`
  - WPF shell
  - MVVM view models
  - page state driven from the connector session snapshot
- `TruckNav.Connector.Core`
  - domain enums
  - stream-state helpers
  - capture/game abstractions for later phases
- `TruckNav.Connector.Networking`
  - persistent connector identity
  - Kestrel host bootstrap
  - protocol validation and serialization
  - mDNS advertisement
  - active-session coordination
- `TruckNav.Connector.Tests`
  - protocol, persistence, session, and integration coverage

### Flutter Mobile

- `lib/features/session`
  - centralized session state
  - persistent mobile identity
  - reconnect policy
  - mDNS discovery plus Android local-network access abstraction
  - WebSocket control client with address fallback and ping/pong smoothing
- `lib/features/discovery`
  - first launch naming
  - LAN explanation
  - connector list / previous connector badge
  - connect / version mismatch / no-PC-found states
- `lib/features/connector`
  - connected shell
  - game-selection shell
  - reconnect / weak-connection states
- `lib/features/gps`
  - fullscreen GPS placeholder stage
  - controls visible/hidden
  - quick menu
  - reconnect overlay

## Actual Dependencies

Pinned and implemented in the repository on August 23, 2026:

### Windows

- `CommunityToolkit.Mvvm` `8.4.2`
- `Microsoft.Extensions.Hosting` `8.0.1`
- `Makaretu.Dns.Multicast.New` `0.38.0`

### Flutter

- `flutter_riverpod` `3.4.2`
- `go_router` `17.5.0`
- `shared_preferences` `2.5.5`
- `multicast_dns` `0.3.3+1`
- `web_socket_channel` `3.0.3`
- `uuid` `4.6.0`

## Port Strategy

The Windows Connector binds Kestrel with `ListenAnyIP(0)`.

That means:

- an ephemeral TCP port is selected at runtime
- IPv4 LAN access is available on all reachable interfaces
- dual-stack IPv6 is available when the host stack supports it
- the diagnostic `ServiceUri` remains loopback-only for local tests and tooling

That port is published through mDNS PTR/SRV/TXT data, so Android never relies on a hardcoded port or stale IP address.

Android resolves all advertised A/AAAA addresses, filters unusable link-local results, prefers practical IPv4 LAN addresses first, and keeps the persistent `deviceId` authoritative.

## Android LAN Access

Android discovery now flows through a `LocalNetworkPermissionService`.

Current repository behavior on August 23, 2026:

- merged release `targetSdkVersion` is `36`
- local network access remains implicitly available through `INTERNET`
- Android multicast discovery acquires a `WifiManager.MulticastLock` only while discovery is active
- the release manifest now includes cleartext LAN support plus multicast permissions needed for Phase 2 `ws://` and mDNS behavior

The abstraction is already prepared for Android 17 / API 37 `ACCESS_LOCAL_NETWORK` runtime enforcement without pushing permission logic down into Widgets.

## State Model Principles

Both shells still use explicit state instead of ad hoc UI booleans for the main product flow.

Examples now in active use:

- desktop `DeviceConnectionState`
- mobile `ConnectionStage`
- version mismatch vs reconnect vs manual disconnect

This keeps discovery, connect, reconnect, and waiting states coherent across screens.

## Missing Design Reference Blocker

The approved HTML reference files are still missing:

- `design-reference/trucknav-mobile.html`
- `design-reference/trucknav-connector.html`

Because of that, this phase only commits:

- theme tokens
- reusable shells
- centralized state wiring
- spec-accurate placeholder behavior

The final visual fidelity pass should begin only after those HTML files are added.
