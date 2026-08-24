# Troubleshooting

## Game Detected But Telemetry Disconnected

- Open Games & Map Data and confirm the plugin reports installed/current.
- Verify `trucksim-gps-telemetry.dll` exists in `<game>\bin\win_x64\plugins`.
- Fully restart the game after installing/updating the plugin.
- Accept the SCS SDK warning on game startup.
- Check Diagnostics for MMF connected, plugin revision, and timestamp.
- `Local\TSGPSTelemetry` exists only while the plugin/game SDK is active; TruckNav never fakes it.

## Steam Game On Another Drive

TruckNav reads Steam registry paths, `libraryfolders.vdf`, and app manifests. If detection still fails, click Choose Folder and select the game root containing `bin\win_x64\eurotrucks2.exe` or `amtrucks.exe`.

## Plugin Installation Fails

- Close ETS2/ATS before installation.
- Confirm the selected game folder is writable.
- The installer touches only the TruckNav DLL and provenance file in `bin\win_x64\plugins`.
- Reinstall Connector if its bundled `ThirdParty\TruckSimGpsTelemetry` DLL/license is missing.

## Navigation Data Build Fails

- Close the game before generation.
- Confirm Node/npm and the external `truckermudgeon/maps` tool are available. Set `TRUCKNAV_MAPS_TOOL` to its local clone when it is not in the documented development location.
- Review `%LOCALAPPDATA%\TruckNav\navigation\<game>\generation.log`.
- The parser needs substantial memory; TruckNav sets Node's old-space limit to 8 GiB.
- Rebuild after game updates or DLC changes when the cache is reported stale.

## Destination Or Route Unavailable

- Select a Freight Market job or accept a delivery. Before the trailer is attached, TruckNav routes to the source depot; after attachment, it switches to the delivery depot. Plain free roam has no destination.
- Destination lookup requires exact game city/company tokens, with exact unambiguous display names as fallback. TruckNav intentionally does not fuzzy-guess a depot.
- A disconnected graph component, unsupported ferry/mod edge, or generator limitation can produce no route; Diagnostics shows the resolved state.

## HEN Says Map Data Unavailable

- Open ROCK's Navigation page and check Visual Map separately from Navigation Graph.
- Click Build Map or Rebuild Map Data for the selected game. The game should be closed if parser data also needs regeneration.
- Confirm `%LOCALAPPDATA%\TruckNav\map\<game>\trucknav-visual-map-v1.bin` exists and is not marked stale.
- Pair HEN again after updating either app so the active session receives a fresh map token.
- TruckNav intentionally does not fall back to sparse routing-graph lines.

## Third-Party Map Mods

Base games and installed official map DLCs are targeted. ProMods and other third-party map mods are not guaranteed and may put the truck/destination on roads absent from the cache. Disable the mod or generate compatible data in a future mod-aware workflow.

## Truck Arrow Offset Or Jitter

- Confirm cache game version/DLC signature matches the running installation.
- A consistent road offset indicates a coordinate/import mismatch; record X/Z and a recognizable location from Diagnostics.
- Short jitter is smoothed over 90 ms. Jumps of 2 km or more intentionally snap as teleport/quick travel.
- Verify the game does not use an unsupported map mod.

## Phone Cannot Connect Or Reconnect

- ROCK and HEN must be on the same LAN; avoid guest/client-isolated Wi-Fi.
- Allow TruckNav Connector on the Windows Private network firewall profile.
- Protocol v2 must be installed on both PC and phone.
- Confirm `_trucknav._tcp.local` discovery and the listener port in Diagnostics.
- Only one mobile session can be active.

## Build Problems

- Use .NET 8+ with Windows SDK 19041+ for WPF.
- Run `dotnet restore apps/windows/TruckNav.Connector.sln` before rebuilding.
- Use Flutter 3.44/Dart 3.12 compatible tooling and Java 17+ for Android release packaging.
