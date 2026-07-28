# Bowire Samples

Sample applications demonstrating [Bowire](https://github.com/Kuestenlogik/Bowire) — the interactive API browser for ASP.NET Core. This repo is the cross-protocol **Harbor flagship**: one shared **Harbor Control Center** domain ([`harbor-demo/`](harbor-demo/)) rendered across every protocol — the side-by-side comparison + the multi-protocol USP — now being reshaped into per-protocol microservices (see [`harbor-demo/REDESIGN.md`](harbor-demo/REDESIGN.md)).

The small per-plugin "hello-world" demos are **not** here — each moved to the repo that owns its plugin ([details below](#where-the-single-plugin-demos-live-now)).

## Harbor demo

All harbor-demo samples share a single **Harbor Control Center** domain (`Ship`, `Dock`, `Crane`, `Container`, `PortCall`) from `Kuestenlogik.Bowire.Samples.Shared`, so every protocol tells the same story with different wire formats.

| Sample | Port | Shows |
|---|---|---|
| **Combined** | 5101 | gRPC + REST + SignalR + WebSocket + SSE against one `HarborStore` — the multi-protocol USP |
| **Grpc** | 5110 | All four gRPC call types (unary / server-stream / client-stream / duplex), trailers, auth metadata |
| **Rest** | 5111 | Full HTTP verb coverage, `ProblemDetails`, multipart upload (`IFormFile`) |
| **SignalR** | 5112 | Invoke, `IAsyncEnumerable<T>` streaming, groups, user-scoped broadcast |
| **WebSocket** | 5113 | Text + binary frames, sub-protocol negotiation, keep-alive |
| **Sse** | 5114 | `Last-Event-ID` resume from a 512-event replay buffer |
| **GraphQL** | 5115 | HotChocolate 15 — query + mutation + subscription, nested resolvers via `[ExtendObjectType]` |
| **OData** | 5116 | OData v4 — `$select`, `$filter`, `$orderby`, `$expand`, `$count` across five entity sets |
| **Mqtt** | 5117 (+ 1883 broker) | Embedded `MqttServerFactory` broker, retained messages, Last Will and Testament |
| **SocketIo** | 5118 | UI shell only (Socket.IO requires a Node.js broker — see the sample's README) |
| **Mcp** | 5119 | AI-invocable `[McpServerTool]` + `[McpServerResource]` via HTTP/SSE transport |
| **AsyncApi** | 5120 (+ Mqtt sample's 1883 broker) | Schema-driven discovery — serves `harbor-events.asyncapi.yaml`; Bowire reads it and routes operations onto the sibling Mqtt sample's broker via the AsyncAPI plugin |

Plus **`Kuestenlogik.Bowire.Samples.Shared`** — the common domain types + seeded `HarborStore` with 3 ships, 5 docks, 3 cranes, 6 containers, 3 port calls. Every other project references it.

## Harbor microservices (redesign, in progress)

The harbor demo is being reshaped from the one-store monolith above into
bounded-context **microservices** — each domain part on the protocol that fits
it, discovered as one landscape. See
[`harbor-demo/REDESIGN.md`](harbor-demo/REDESIGN.md). Landed so far:

| Service | Context | Protocol | Port |
|---|---|---|---|
| **Fleet** | vessel registry (master data) | gRPC | 5150 |
| **Inventory** | docks + crane config | OData | 5151 |
| **Gate** | container gate-in/out lifecycle | REST | 5152 |
| **PortCalls** | port-call orchestration (BFF over Fleet + Inventory + Gate) | GraphQL | 5153 |
| **Tracking** | raw AIS position ingress | WebSocket | 5154 |
| **Operations** | operator console egress (consumes Tracking) | SignalR | 5155 |
| **Arrivals** | public arrivals board (CQRS read-model) | SSE | 5156 |
| **Telemetry** | crane device bus + its own AsyncAPI schema | MQTT (broker :1883) + AsyncAPI | 5157 |
| **Assistant** | AI ops assistant fronting the services | MCP | 5158 |
| **Gateway** | one workbench over all services (catalogue discovery) | Bowire | 5159 |

`PortCalls` is a BFF: a single `portCall(id) { ship dock containers }` query
fans out to Fleet (gRPC), Inventory (OData) and Gate (REST) — one query, three
wires. The live cascade shows the deliberate WS-vs-SignalR contrast: `Tracking`
is a bare WebSocket AIS ingress, `Operations` re-emits it with SignalR's
group/stream/broadcast features, and `Arrivals` is the resumable SSE read-model.
`Telemetry` runs an embedded MQTT broker for live crane status (the live half
of the Crane shared-kernel split — Inventory owns the static config on the same
`CraneId`) and serves its own AsyncAPI schema; `Assistant` exposes the whole
landscape as MCP tools an AI agent can call.

**One-command boot** — the Aspire AppHost starts the entire landscape (each
service on its fixed catalogue port) and gives you the dashboard for logs:

```bash
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.AppHost
```

Then browse everything together at `https://localhost:5159/bowire`.

**The correlated timeline** — the money-shot recording at
[`harbor-demo/recordings/port-call-1.bowire-recording.json`](harbor-demo/recordings/port-call-1.bowire-recording.json)
captures **one port call (id 1, ship 101 "Nordstern", dock 1, crane 1) across
all eight wires** — gRPC, OData, REST, GraphQL, WebSocket, SignalR, SSE, MQTT —
keyed by the same shared ids, so the timeline reads as a single business event
crossing the whole landscape. Validate it with `bowire recording validate`,
replay its unary steps against the running landscape from the workbench's
Recordings rail, or re-serve the recorded surface with
`bowire mock --recording harbor-demo/recordings/port-call-1.bowire-recording.json`.

What the mock serves from this file: the REST, OData and GraphQL steps
answer plain HTTP calls, the WebSocket / SignalR / SSE steps replay their
captured frames on the recorded cadence, and the MQTT step publishes
through the mock's embedded broker. The gRPC step carries no
`responseBinary` wire bytes (the file is hand-curated), so the mock
lists it as not replayable at startup — record against the live Fleet
service to capture a servable gRPC step.

## Domain

```text
 Ship          a vessel (container / bulk / tanker)
 Dock          a berth, optionally with a crane
 Crane         attached to a dock, Idle / Working / Maintenance / OutOfService
 Container     stored / loading / on-board
 PortCall      Scheduled -> Approaching -> Docked -> Departing -> Completed  (or Cancelled)
```

## Running

Build everything once:

```bash
dotnet build Bowire.Samples.slnx
```

Then launch any sample and browse the Bowire UI at `/bowire`:

```bash
dotnet run --project src/Kuestenlogik.Bowire.Samples.Combined     # https://localhost:5101/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.Grpc         # https://localhost:5110/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.Rest         # https://localhost:5111/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.SignalR      # https://localhost:5112/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.WebSocket    # https://localhost:5113/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.Sse          # https://localhost:5114/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.GraphQL      # https://localhost:5115/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.OData        # https://localhost:5116/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.Mqtt         # https://localhost:5117/bowire (broker on :1883)
dotnet run --project src/Kuestenlogik.Bowire.Samples.SocketIo     # https://localhost:5118/bowire
dotnet run --project src/Kuestenlogik.Bowire.Samples.Mcp          # https://localhost:5119/bowire
```

## Prerequisites: pack Bowire locally

Samples reference `Kuestenlogik.Bowire` + `Kuestenlogik.Bowire.Protocol.*` as **NuGet packages**, not `ProjectReference`. The shipped `nuget.config` wires a `local` feed at `../Bowire/artifacts/packages/`, and per-project package versions are pinned to `0.9.4`.

To repack with a newer version, bump the `Version=` below and update the `PackageReference` in each csproj:

```bash
cd ../Bowire
dotnet pack Kuestenlogik.Bowire.slnx -c Release -p:Version=0.9.4
```

## Per-sample detail

Each sample directory ships its own README with the method list, minimum-viable wiring snippet, and the protocol-specific features it exercises.

## Where the single-plugin demos live now

There's no `protocols/` folder here anymore — every single-plugin demo moved to the repo that **owns its plugin**, as a _combined_ server + embedded-workbench sample:

- **Monorepo protocols** → the main **Bowire** repo's [`samples/`](https://github.com/Kuestenlogik/Bowire/tree/main/samples): the HTTP/gRPC ones (Rest, gRPC, GraphQL, OData, JSON-RPC, SignalR, SSE, WebSocket, SOAP, MCP), the message brokers (MQTT self-contained; NATS + Pulsar with their own `docker-compose.yml`), and the Node.js Socket.IO server (`samples/socketio-chat`).
- **Sibling-plugin protocols** → each `Bowire.Protocol.*` repo's own `samples/` folder (Akka, Amqp, Dis, Kafka, Surgewave, TacticalApi, Udp).

Each .NET one both serves its protocol **and** mounts `/bowire`, so it doubles as a discovery target and a hosting demo.

(History: migrated from `Kuestenlogik/Bowire/examples/` in 2026-06 to consolidate every sample axis into one repo; the single-plugin demos then moved to their owning plugin's repo in 2026-07 as combined embedded samples, dissolving the `protocols/` folder — this repo now holds only the Harbor flagship.)
