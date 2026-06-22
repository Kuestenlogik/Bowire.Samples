# Kuestenlogik.Bowire.Samples.Grpc

Isolated **gRPC-only** sample. Same harbor domain as the Combined sample,
but every other protocol surface is stripped away so you can see a
minimal drop-in for an existing gRPC service.

## What this sample demonstrates

- **All four gRPC call types** on a single `HarborService`:
  - `SchedulePortCall` — **Unary** (request/reply)
  - `WatchCrane` — **Server streaming** (1 request &rarr; N replies)
  - `UploadManifest` — **Client streaming** (N requests &rarr; 1 reply)
  - `HarborRadio` — **Bidirectional streaming** (interleaved streams)
- **Server Reflection** — registered automatically by `AddBowire()`. That
  is how Bowire enumerates services + methods without a config file.
- **Custom metadata** — `SchedulePortCall` requires an
  `x-dispatcher-id` header and echoes it back as
  `x-echoed-dispatcher`. The request fails with
  `Unauthenticated` when the header is missing &mdash; a good starting
  point for JWT / API-key patterns.
- **Response trailers** — `UploadManifest` emits `x-received-count` and
  `x-accepted-count` trailers. Bowire surfaces them in the response
  metadata tab.
- **Error status codes** &mdash; `NotFound` when the ship / dock / crane
  id is unknown.

## Run it

```bash
dotnet run --project samples/Kuestenlogik.Bowire.Samples.Grpc
```

Open [https://localhost:5110/bowire](https://localhost:5110/bowire) in
a browser. The Bowire UI will list `HarborService` in the sidebar with
four method entries tagged **U**, **SS**, **CS**, **DX**.

## Minimum viable setup

If you copy this sample, the only Bowire-specific lines are:

```csharp
builder.Services.AddGrpc();
builder.Services.AddBowire();          // <-- picks up the gRPC plugin
// ...
app.MapGrpcService<MyService>();
app.MapBowire();                       // <-- mounts /bowire
```

That's it. Reflection, discovery, and the request form are handled by
`Kuestenlogik.Bowire.Protocol.Grpc`.

## Verify gRPC-Web actually works (not just configured)

`UseGrpcWeb(DefaultEnabled = true)` in `Program.cs` *enables* the
HTTP/1.1 transport, but the only way to be sure it's wired up is to
talk to it. From a second terminal, with the sample running on
`https://localhost:5110`, point the standalone `bowire` CLI at the
gRPC-Web endpoint using the `grpcweb@` URL-hint prefix:

```bash
# Native HTTP/2 transport (default — no hint required)
bowire --url grpc@https://localhost:5110

# gRPC-Web over HTTP/1.1 transport (same server, same service)
bowire --url grpcweb@https://localhost:5110
```

Both invocations should produce identical discovery output:

```
1 services discovered:
  harbor.HarborService (6 methods)
```

The six methods are every RPC declared in `harbor.proto`:
`SchedulePortCall`, `GetPortCall`, `ListPortCalls`, `WatchCrane`,
`UploadManifest`, `HarborRadio`. (The reflection service itself is
filtered out of the discovery listing.) An **identical method count
across both transports** is the proof — if gRPC-Web were
misconfigured the HTTP/1.1 path would either 415 the reflection
probe or return zero methods.

Browser callers (which need CORS preflight) would additionally require
`app.UseCors(...)` **before** `app.UseGrpcWeb(...)`. Bowire's own
workbench talks server-to-server, so CORS is intentionally off in this
sample.
