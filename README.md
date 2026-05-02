# Bowire Samples

Sample applications demonstrating [Bowire](https://github.com/Kuestenlogik/Bowire) — the interactive API browser for ASP.NET Core.

All samples share a single **Harbor Control Center** domain (`Ship`, `Dock`, `Crane`, `Container`, `PortCall`) from `Kuestenlogik.Bowire.Samples.Shared`, so every protocol tells the same story with different wire formats.

## Samples

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
