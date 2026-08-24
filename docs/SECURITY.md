# Security And Privacy

TruckNav is LAN-first and local-only:

- no cloud backend, account, analytics, telemetry upload, or external routing service
- no arbitrary filesystem HTTP endpoint
- no shell, remote keyboard, or game-input execution
- client JSON is typed, validated, versioned, and limited to 16 KiB
- one active mobile session plus one pending handshake reservation
- persistent UUID identity rather than IP/MAC identity
- Android reconnects through rediscovery
- plugin installation targets only the selected game's `bin\win_x64\plugins` folder and refuses while the game is running
- map generation reads local game geometry and writes graph data under `%LOCALAPPDATA%\TruckNav\navigation` and visual data under `%LOCALAPPDATA%\TruckNav\map`
- read-only map HTTP endpoints require a random 256-bit token issued by, and scoped to, the active paired WebSocket session
- map routes accept only a supported game, numeric LOD, and numeric tile coordinates; they cannot expose arbitrary files

The current local transport is cleartext HTTP/WebSocket and should be treated as trusted-private-LAN traffic, not Internet-safe authentication or encryption. Do not expose the Connector port through router forwarding.
