# Bowire Samples

Sample applications demonstrating [Bowire](https://github.com/Kuestenlogik/Bowire) — the interactive API browser for ASP.NET Core. The repo holds two complementary axes:

| Axis | Folder | Story |
|---|---|---|
| **Harbor demo** | [`harbor-demo/`](harbor-demo/) | One shared **Harbor Control Center** domain implemented across every protocol. Side-by-side comparison of how Bowire renders gRPC vs REST vs SignalR vs … against the *same* business model. Powers the marketing-site screenshots + the multi-protocol USP. |
| **Protocols** | [`protocols/`](protocols/) | Per-plugin canonical demos using each protocol's classical example (PetStore for REST, Greeter for gRPC, Northwind for OData, Math for JSON-RPC, &c). Tiny, single-purpose, focused on testing one plugin. Linked from `bowire.io/docs/protocols/*.md`. |

Pick `harbor-demo/` when you want to *compare* protocols. Pick `protocols/` when you want to *test or learn* one plugin in isolation.

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

## Protocols (per-plugin canonical demos)

The [`protocols/`](protocols/) folder holds the per-plugin canonical demos — each one boots a tiny, single-purpose target Bowire can connect to so you can exercise a protocol plugin without standing up the full Harbor domain.

| Sample | Plugin | How to run | Connect from Bowire |
|--------|--------|------------|---------------------|
| [`Soap.CalculatorService`](protocols/Soap.CalculatorService) | `Bowire.Protocol.Soap` | `dotnet run` | `http://localhost:5180/Calculator.asmx` |
| [`Pulsar`](protocols/Pulsar) + [`Pulsar.Producer`](protocols/Pulsar.Producer) | `Bowire.Protocol.Pulsar` | `docker compose up` + `dotnet run --project Pulsar.Producer` | `pulsar://localhost:6650` |
| [`Rest.PetStore`](protocols/Rest.PetStore) | `Bowire.Protocol.Rest` | `dotnet run` | `http://localhost:5181` |
| [`Grpc.Greeter`](protocols/Grpc.Greeter) | `Bowire.Protocol.Grpc` | `dotnet run` | `http://localhost:5182` |
| [`GraphQL.Books`](protocols/GraphQL.Books) | `Bowire.Protocol.GraphQL` | `dotnet run` | `http://localhost:5183/graphql` |
| [`SignalR.Chat`](protocols/SignalR.Chat) | `Bowire.Protocol.SignalR` | `dotnet run` | `http://localhost:5184/chathub` |
| [`WebSocket.Echo`](protocols/WebSocket.Echo) | `Bowire.Protocol.WebSocket` | `dotnet run` | `ws://localhost:5185/ws` |
| [`Sse.Ticker`](protocols/Sse.Ticker) | `Bowire.Protocol.Sse` | `dotnet run` | `http://localhost:5186/events` |
| [`JsonRpc.Math`](protocols/JsonRpc.Math) | `Bowire.Protocol.JsonRpc` | `dotnet run` | `http://localhost:5187/rpc` |
| [`OData.Northwind`](protocols/OData.Northwind) | `Bowire.Protocol.OData` | `dotnet run` | `http://localhost:5188/odata` |
| [`SocketIo.Chat`](protocols/SocketIo.Chat) | `Bowire.Protocol.SocketIo` | `npm start` | `http://localhost:5189` |
| [`Nats`](protocols/Nats) | `Bowire.Protocol.Nats` | `docker compose up` | `nats://localhost:4222` |
| [`Mqtt`](protocols/Mqtt) | `Bowire.Protocol.Mqtt` | `docker compose up` | `tcp://localhost:1883` |
| [`Mcp.Tools`](protocols/Mcp.Tools) | `Bowire.Protocol.Mcp` | `dotnet run` | `http://localhost:5190/mcp` |

Ports stay in the 5180–5199 band so a protocol-demo and the Harbor multi-sample (5101–5120) don't collide when both run.

(Migrated from `Kuestenlogik/Bowire/examples/` in 2026-06 to consolidate every sample axis into one repo. The marketing site + `docs/protocols/*.md` reference the new paths under `protocols/`.)
