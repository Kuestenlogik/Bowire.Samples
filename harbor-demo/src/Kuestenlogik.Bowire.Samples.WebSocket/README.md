# Kuestenlogik.Bowire.Samples.WebSocket

Isolated **WebSocket-only** sample on `https://localhost:5113/bowire`.

## What this demonstrates

- **Text frames** — `/ws/ship-tracker` streams JSON positions once a second per ship
- **Binary frames** — `/ws/manifest-upload` accepts raw bytes until EndOfMessage, then replies with a text-frame summary
- **Sub-protocol negotiation** — ship-tracker accepts `bowire-ais.v1` via `Sec-WebSocket-Protocol`
- **Keep-alive** — `WebSocketOptions.KeepAliveInterval = 30s` so NAT paths don't drop the socket
- **Close codes** — binary endpoint closes with `InvalidMessageType` if the client sends text

## Minimum viable setup

```csharp
builder.Services.AddBowire();
app.UseWebSockets();
app.MapGet("/ws/my-stream", async ctx => { /* handler */ });
app.MapBowire();
```
