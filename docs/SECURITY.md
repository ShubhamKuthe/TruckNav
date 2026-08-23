# Security

TruckNav Phase 2 is LAN-only and intentionally narrow.

## Implemented Principles

- no cloud backend
- no account system
- no remote shell
- no file transfer
- no arbitrary command execution
- no arbitrary key injection

## Current Trust Model

Phase 2 uses a temporary local-trust model:

- each peer creates a persistent UUID locally
- identity is never based on IP, MAC address, or hostname
- Android reconnects only after rediscovery, not from stale address memory

This is a local signaling/control foundation, not the final production transport story.

## Validation and Limits

All incoming control messages are validated for:

- message type
- JSON shape
- required fields
- protocol compatibility
- role/platform correctness
- maximum control payload size (`16 KB`)

Malformed or incompatible payloads are rejected with typed protocol errors and the socket is closed where appropriate.

## Session Scope

The current WebSocket channel is limited to:

- `hello`
- `hello_ack`
- `ping`
- `pong`
- typed `error`

Phase 2 deliberately does not expose:

- shell commands
- file APIs
- raw keyboard APIs
- arbitrary game input hooks

## Phase 2 Transport Caveat

Phase 2 still uses cleartext local HTTP / WebSocket control traffic on the LAN.

The Android release manifest now explicitly permits this so the current `ws://` control lane works on modern Android targets, but this is a temporary Phase 2 compatibility choice rather than the desired long-term transport model.

## Future Media Security

Production media and richer control transport are still planned for WebRTC in later phases.

Until that ships, the repository should not claim end-to-end encrypted media transport or a final hardened pairing model.
