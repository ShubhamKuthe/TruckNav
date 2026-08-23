# Testing

## Windows Coverage

Current automated Windows coverage includes:

- protocol serializer round-trips
- protocol version compatibility
- hello validation
- malformed JSON rejection
- missing required field rejection
- protocol mismatch rejection
- `/health` and `/info` integration
- `hello_ack` integration
- `ping` / `pong` integration
- one-active-session busy handling
- LAN IPv4 reachability for `/health`, `/info`, and `/ws` when a non-loopback address is available
- mDNS service-profile metadata and TXT-key validation
- pending-handshake reservation concurrency
- persistent connector identity
- intentional vs unexpected disconnect state
- existing capture/math/state helper tests from Phase 1

## Flutter Coverage

Current automated Flutter coverage includes:

- centralized session controller discovery flow
- deduplication by `deviceId`
- previous connector badge state
- handshake success
- protocol mismatch state
- reconnect state after unexpected disconnect
- dedicated local-network-access-required state
- connector address ranking and IPv4/IPv6 fallback behavior
- no-PC-found recovery via Scan Again
- discovery screen rendering
- GPS controls visible/hidden behavior

Discovery and connection dependencies are abstracted so tests do not require a real LAN.

## Manual End-to-End Status

The real ROCK ↔ HEN physical LAN workflow has not been executed from this repository session.

Current validation is automated only.

The real manual checklist still pending on hardware is:

1. Run TruckNav Connector on Windows.
2. Confirm the Connector logs show startup, listener port, reachable LAN interfaces, and mDNS advertisement start.
3. Install the Android build on a physical device.
4. Put ROCK and HEN on the same LAN.
5. Open TruckNav on Android.
6. Confirm ROCK appears automatically with no manual IP entry.
7. Tap Connect.
8. Confirm hello / hello_ack on both sides.
9. Confirm Windows Home shows HEN connected.
10. Confirm Android enters the connected flow.
11. Turn HEN Wi-Fi off temporarily.
12. Confirm Android enters reconnecting.
13. Restore Wi-Fi.
14. Confirm automatic reconnect.
15. Tap intentional Disconnect.
16. Confirm Android does not immediately reconnect.
