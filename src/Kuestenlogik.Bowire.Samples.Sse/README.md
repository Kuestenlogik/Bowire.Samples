# Kuestenlogik.Bowire.Samples.Sse

Isolated **Server-Sent Events** sample on `https://localhost:5114/bowire`.

## What this demonstrates

- **Named events** — `event: port-call-changed`, `event: heartbeat`
- **Event IDs** — monotonic `id: N` on every frame
- **Last-Event-ID resume** — reconnecting client sends `Last-Event-ID`, server replays buffered events (last 512) before going live
- **Keep-alive via heartbeats** — `/events/heartbeat` ticks every 5 seconds so clients can confirm the socket is still up when port-call traffic goes quiet

## Endpoints

| Path | Event type | Frequency |
|------|-----------|-----------|
| `/events/port-calls` | `port-call-changed` | whenever `HarborStore.PortCallChanged` fires |
| `/events/heartbeat`  | `heartbeat`        | every 5 seconds |

## Minimum viable setup

```csharp
builder.Services.AddBowire();

app.MapGet("/events/my-stream", async ctx => {
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    await ctx.Response.WriteAsync("id: 1\nevent: my-event\ndata: {...}\n\n");
});
app.MapBowire();
```
