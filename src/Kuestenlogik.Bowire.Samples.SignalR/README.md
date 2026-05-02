# Kuestenlogik.Bowire.Samples.SignalR

Isolated **SignalR-only** sample. Runs on `https://localhost:5112/bowire`.

## What this demonstrates

- **Hub Invoke** — `GetPortCall(id)` returns a value to the caller
- **Server streaming** — `SubscribeToChanges()` yields every future
  port-call change via `IAsyncEnumerable<PortCall>`
- **Client streaming** — `PushEtaUpdates()` accepts an async stream of
  ETA strings from the caller
- **Groups** — `JoinDock(dockNumber)` / `LeaveDock(dockNumber)` so a
  dispatcher can subscribe to updates for a single dock
- **Users** — `Clients.User(userId)` fans events to one identified
  connection
- **Broadcast** — `Clients.All.SendAsync(...)` reaches every connected
  client

All three fan-out targets fire from the same method
(`FakeStatusChange`) so you can observe them back-to-back from the
Bowire UI.

## Minimum viable setup

```csharp
builder.Services.AddSignalR();
builder.Services.AddBowire();

app.MapHub<MyHub>("/hubs/my-hub");
app.MapBowire();
```
