# Bowire Samples

Sample applications demonstrating [Bowire](https://github.com/Kuestenlogik/Bowire) — the interactive API browser for ASP.NET Core. The repo holds two complementary axes:

| Axis | Folder | Story |
|---|---|---|
| **Harbor demo** | [`harbor-demo/`](harbor-demo/) | One shared **Harbor Control Center** domain implemented across every protocol. Side-by-side comparison of how Bowire renders gRPC vs REST vs SignalR vs … against the *same* business model. Powers the marketing-site screenshots + the multi-protocol USP. |
| **Protocols** | [`protocols/`](protocols/) | Just the TacticalAPI radar server, until it moves to its own repo. Every other single-plugin demo moved to the repo that owns its plugin — the monorepo protocols to the main repo's [`samples/`](https://github.com/Kuestenlogik/Bowire/tree/main/samples). |

Pick `harbor-demo/` when you want to *compare* protocols. For a single plugin in isolation, use the combined `Kuestenlogik.Bowire.Sample.*` demos in the [main repo's `samples/`](https://github.com/Kuestenlogik/Bowire/tree/main/samples); `protocols/` here keeps only the TacticalAPI demo for now.

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
| **Gateway** | one workbench over all services (catalogue discovery) | Bowire | 5159 |

Run the services, then the gateway, and browse them together at
`https://localhost:5159/bowire`.

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

## Protocols (external-infra demos)

The [`protocols/`](protocols/) folder now holds only the TacticalAPI radar server, and only until it moves to its own repo (`Bowire.Protocol.TacticalApi/samples/`).

| Sample | Plugin | How to run | Connect from Bowire |
|--------|--------|------------|---------------------|
| [`TacticalApi.RadarSweep`](protocols/TacticalApi.RadarSweep) | `Bowire.Protocol.TacticalApi` | `dotnet run` | `http://localhost:5191` |

Every single-plugin demo moved to the repo that owns its plugin, as a _combined_ server + embedded-workbench sample. The main **Bowire** repo's [`samples/`](https://github.com/Kuestenlogik/Bowire/tree/main/samples) took the monorepo protocols — the HTTP/gRPC ones (Rest, gRPC, GraphQL, OData, JSON-RPC, SignalR, SSE, WebSocket, SOAP, MCP), the message brokers (MQTT self-contained; NATS + Pulsar with their own `docker-compose.yml`), and the Node.js Socket.IO server (`samples/socketio-chat`). Each .NET one both serves its protocol **and** mounts `/bowire`, so it doubles as a discovery target and a hosting demo.

(History: migrated from `Kuestenlogik/Bowire/examples/` in 2026-06 to consolidate every sample axis into one repo; the single-plugin demos moved to their owning plugin's repo in 2026-07 as combined embedded samples, leaving only the TacticalAPI demo here until its own repo's sample lands.)
