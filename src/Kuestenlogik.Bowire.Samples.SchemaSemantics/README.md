# Kuestenlogik.Bowire.Samples.SchemaSemantics

A deliberately plain-vanilla gRPC server that demonstrates **Bowire's
frame-semantics framework end-to-end with zero Bowire-side code**.

## What this sample proves

When you point Bowire 1.3.0+ at this server and invoke `WatchShips`,
the workbench should:

1. Discover the `Ships` service via gRPC reflection.
2. Start the server-streaming subscription.
3. Notice that incoming `ShipUpdate` frames carry conventionally-named
   `lat` + `lng` fields whose values sit inside WGS84 range.
4. **Auto-mount a Map tab** next to the streaming-frames pane.
5. Render every incoming ship position as a pin, live, with one pin
   per ship and live-tracking updates per Hertz.

Look at what this sample does **not** ship:

- No `IBowireSchemaHints` implementation.
- No `bowire.schema-hints.json` checked into the repo.
- No `[BowireExtension]` C# class.
- No annotations beyond the proto file itself.

The map widget appears purely because Bowire's built-in
`Wgs84CoordinateDetector` matches the field names + value ranges. That
is the pgAdmin pattern: shape-of-data drives viewer choice, not
protocol-author opt-in.

## Run it

```bash
# Terminal 1 — start the sample.
dotnet run --project src/Kuestenlogik.Bowire.Samples.SchemaSemantics

# Terminal 2 — point a standalone bowire at the sample.
# Native HTTP/2 transport:
bowire --url grpc@https://localhost:5111
# …or gRPC-Web over HTTP/1.1:
bowire --url grpcweb@https://localhost:5111
```

Open the URL the bowire CLI prints, expand the `schemasemantics.Ships`
service in the sidebar, click `WatchShips`, hit Execute.

## What you should see

The streaming-frames pane on the left fills with `ShipUpdate` events
at 1 Hz, three ships cycling round-robin (Aurora, Helgoland-Express,
Containerschiff-7). Hamburg-Harbour coordinates, jittered slightly per
tick so the pins drift live instead of sitting still.

On the right (or below, depending on your layout preference) the
auto-mounted **Map tab** shows pins for every received frame, layered
by ship. Multi-select frames in the streaming list (Ctrl/Shift-click)
and the map flies to fit the selection.

## Why no Bowire references in the csproj

This sample is intentionally not a Bowire-host — it's a plain gRPC
server, the same way a real TacticalAPI deployment would be. Bowire
runs as a separate process you point at the server. The split keeps
the demo honest: nothing in the server's source code knows about the
map widget; the framework finds the coordinates by content alone.

If you want the embedded-mode version (one process serving both the
gRPC API and the Bowire workbench at `/bowire`), see the sibling
`Kuestenlogik.Bowire.Samples.Grpc` sample.

## Bowire version requirements

- **1.2.x** is fine for connecting and seeing the streaming frames in
  the workbench.
- **1.3.0+** is required for the auto-mounted map widget — the
  frame-semantics framework lands there. Earlier versions show the
  frames in the streaming pane but no map.

## Variations to try

The detector ships strict by design — both `lat` and `lng` (or `long`
/ `longitude`) names must match and the value must sit in the WGS84
range. Try editing `Protos/ships.proto`:

- Rename `lng` to `lon` — the regex doesn't match `lon` alone, so the
  map widget won't auto-mount. Right-click the field in Bowire and
  mark it as `coordinate.longitude` manually, persist for the session.
- Add a `breitengrad` field — German for latitude. Auto-detection
  won't fire. Right-click marks it.
- Persist the manual marks to `bowire.schema-hints.json` so the next
  bowire restart against the same server doesn't need the marks
  reapplied.

Each variation is a tiny demonstration of the three-tier resolution
priority: User → Plugin hint → Auto-detector.
