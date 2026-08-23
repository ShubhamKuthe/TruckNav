# TruckNav Protocol

## Versioning

- manifest version: `1`
- protocol version and app version remain separate

Phase 2 peers expose:

- `protocolVersion`
- `appVersion`
- `deviceId`
- `deviceName`
- `role`
- `platform`
- `capabilities`

## Discovery

Windows registers the service with Makaretu as `_trucknav._tcp`, which the library exposes as the DNS-SD browse type `_trucknav._tcp.local`.

Published metadata:

- `deviceId`
- `deviceName`
- `protocolVersion`
- `appVersion`
- `role`
- `capabilities`

Android discovery queries `ResourceRecordQuery.serverPointer('_trucknav._tcp.local')` and then resolves:

- PTR service instance records
- SRV target + port
- TXT metadata
- A/AAAA address records for the current host

The Android client keeps every usable resolved address for a Connector, prefers practical IPv4 LAN addresses first, and ignores unusable IPv6 link-local results instead of failing on the first address returned.

The normal UI does not show raw IP addresses.

## HTTP Surface

Phase 2 Connector endpoints:

- `GET /health`
- `GET /info`
- `WebSocket /ws`

`/health` returns:

```json
{
  "status": "ok"
}
```

`/info` returns the public connector identity payload.

## Hello Handshake

Android sends `hello` immediately after the WebSocket opens.

Example:

```json
{
  "type": "hello",
  "protocolVersion": 1,
  "appVersion": "0.1.0",
  "deviceId": "7ac8fc8b-9fd6-4acf-a451-e0c4d3b5a920",
  "deviceName": "HEN",
  "role": "mobile",
  "platform": "android",
  "capabilities": []
}
```

If valid, Windows responds with `hello_ack` carrying its corresponding connector identity.

If invalid, Windows returns a typed `error` and closes the socket.

## Validation Rules

Phase 2 validates:

- JSON syntax
- required `type`
- compatible protocol version
- UUID format for `deviceId`
- required string fields
- allowed `role` / `platform`
- reasonable capability count and field lengths
- maximum control message size of `16 KB`

## Connection Health

Implemented Phase 2 health messages:

- `ping`
- `pong`

Android sends lightweight pings on an interval.

Weak-connection UI no longer flips on a single slow sample.

Temporary Phase 2 thresholds now live in code as explicit constants:

- ping interval: `10s`
- weak RTT threshold: `1200ms`
- weak state after `2` consecutive degraded pong samples
- healthy state after `2` consecutive recovered pong samples
- pong timeout / disconnect: `25s`

## Active Session Rule

The Connector allows one active mobile session and one pending validated handshake reservation.

If a second client attempts to connect while one is active, the Connector returns a typed:

- `busy`

and preserves the existing session.

## Errors

Typed errors currently exercised in code and tests:

- `malformed_json`
- `invalid_payload`
- `missing_required_fields`
- `protocol_mismatch`
- `unsupported_message_type`
- `message_too_large`
- `busy`
