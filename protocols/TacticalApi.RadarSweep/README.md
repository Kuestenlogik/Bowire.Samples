# TacticalApi.RadarSweep

Canonical mini-server for Rheinmetall's TacticalAPI. Three MIL-2525C
contacts orbit a radar centre at **54.00°N / 11.50°E** (off the German
Baltic coast) at a constant 360°/min. Read-only — the goal is the
smallest runnable target the `Bowire.Protocol.TacticalApi` plugin can
discover + invoke against, not the full Harbor Control Center scene.

## Run

```bash
dotnet run --project protocols/TacticalApi.RadarSweep
```

Listens on `http://localhost:5191` (HTTP/2, no TLS for the canonical
demo).

## Connect from Bowire

```bash
bowire --url http://localhost:5191
```

Pick the `Situation` service. Two methods to exercise:

- **`GetSituationObjects`** (unary) — current snapshot of all three
  contacts. Filter / pagination fields go unused.
- **`SubscribeSituationObjectEvents`** (server-streaming) — fresh
  snapshot every two seconds while the stream is open. The contacts
  rotate around the centre clockwise so the workbench's frame list
  shows monotonic position deltas, easy to verify visually.

The mutation RPCs (`AddOrUpdateSituationObjects`, `DeleteSituationObjects`)
are intentionally **not** implemented here — see the Harbor sample
([`harbor-demo/src/Kuestenlogik.Bowire.Samples.TacticalApi/`](../../harbor-demo/src/Kuestenlogik.Bowire.Samples.TacticalApi/))
for the full read-write surface.

## What this sample shows

| Pattern | Where |
|---|---|
| Build-time fetch of the upstream `.proto` (Apache-2.0 repo, EPL-2.0 / BSD-3-Clause proto sources stay external) | `RadarSweep.csproj` `DownloadTacticalApiProtos` target |
| Bowire plugin uses the bundled `.proto` for discovery — server-side reflection is optional | `Program.cs` `MapGrpcReflectionService` (added but not required) |
| Channel-based server-stream broadcast across multiple subscribers | `SituationServiceImpl.SubscribeSituationObjectEvents` |
| Deterministic motion model (no random walk) | `SituationServiceImpl.AdvanceTracks` |

## Schema source

The `.proto` files are fetched at build time from
`github.com/Rheinmetall/tacticalapi` (pinned to commit
`e68546809d981cd649325dba4a9702c1a77a1a0b`). Re-pin in lock-step with
`Bowire.Protocol.TacticalApi.TacticalApiProtoSha` so client and server
share wire shape.
