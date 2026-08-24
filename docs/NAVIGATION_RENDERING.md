# Native Navigation Rendering

## Heading Convention

TruckNav uses one canonical navigation bearing throughout its domain model and protocol:

- SCS SDK input is a unit value in `[0,1)`, measured counter-clockwise from above.
- SCS `0` is north/forward, `0.25` is west, `0.5` is south, and `0.75` is east.
- In SCS world coordinates, north is `-Z`, east is `+X`, south is `+Z`, and west is `-X`.
- `ScsHeadingConverter.ToNavigationDegrees` converts the SDK value to a clockwise bearing: `0` north, `90` east, `180` south, `270` west.
- Flutter Canvas uses `+X` right and `+Y` down. World Z is therefore projected directly to Canvas Y: north/`-Z` appears upward.
- Heading-up rotates the world by the negative canonical heading. The marker sprite intrinsically points up and receives no second rotation.
- World-up leaves the world unrotated and rotates the marker clockwise by the canonical heading.

The old renderer inverted world Z and rotated the camera by positive heading. Together those operations placed the forward route below the truck for every heading.

## Follow Camera

The heading-up camera follows a target ahead of the real telemetry position. The default follow scale is `0.90` screen pixels per SCS world metre, with the truck horizontally centered at 68% of viewport height. This immediate-road driving view is the maximum zoom; speed adaptation and manual gestures can only zoom outward. The visual marker is never snapped to the route and remains visible in Free Roam without an active route.

Manual pinch or button zoom is continuous between `0.25` and `1.0`. Pan and zoom remain in Free Look until the driver explicitly presses Recenter. Adaptive zoom changes slowly for speed and upcoming maneuver distance only while normal follow is active.

## Route Presentation

- Remaining route: dark casing with cyan `#22D4E5` center and pale cyan highlight.
- Travelled route: muted blue charcoal `#334657`, limited to a short tail in follow mode.
- Direction: white chevrons sampled along the remaining route tangent.
- Endpoints: the route start uses a green flag and the destination uses a red flag.
- Geometry: logical route points remain untouched; only the screen-space path uses bounded corner smoothing.
- Context: a dedicated installed-game visual dataset supplies curved roads, prefab map-point geometry, map areas, dividers, ferry/train links, cities, and POIs. Routing graph edges are not painted as the Release base map.
- Prefabs: local neighbor-connected map points are transformed by the placement origin/rotation. Roundabout rings and all exits, intersection branches, ramps, merges, service areas, and company entrances remain visible.
- Verified POIs: parser coordinates are mapped to licensed Material symbols for fuel, sleep areas, service, garage, dealer, recruitment, company, ferry/train, and weigh stations. Selecting a category filters the real SCS points and reveals available labels.
- Visual hierarchy: map areas sit below class-ordered roads; close level renders every minor road, prefab intersection, roundabout branch, divider, and road-width distinction. Major roads receive close-view centre markings, and transport links retain distinct ferry/train styling.
- Chrome: maneuver, speed-limit, speed, remaining distance, ETA, and trip time stay at viewport edges. The horizontally scrollable map dock exposes all supported POI categories.
- Appearance: a persistent sun/moon control switches the local vector map between dark and light palettes.
- Performance: road and polygon paths are built once when a tile is decoded. Camera movement applies one canvas matrix instead of reprojecting every geometry point, tile snapshots remain stable between loads, and missing local-LAN tiles load in bounded parallel batches.

## Physical Acceptance Checklist

1. Stationary: marker is lower-center, points with the cab, and the camera does not jitter.
2. Straight road: the red route extends mainly above the marker; the short route tail is grey.
3. Right and left turns: map rotation and maneuver icon agree with the visual road.
4. Pinch apart/together: zoom moves smoothly in the standard direction without a message.
5. Manual zoom/pan: Free Look remains unchanged until Recenter is pressed.
6. Overview and Recenter: overview fits the trip; Recenter restores heading-up follow.
7. Wrong turn: real marker remains off-route, then the replacement cyan route appears ahead.
8. Route Advisor hidden: telemetry, marker, route progress, and rerouting continue normally.
9. Roundabout: the ring, approach, all visible exits, and highlighted selected exit are present.
10. Missing map cache: `Map data unavailable` appears; sparse graph context never replaces it.
