# Kuestenlogik.Bowire.Samples.SocketIo

Isolated **Socket.IO** sample. Bundles a minimal Node.js server (`server.js`)
plus the matching Bowire CLI invocation. Browse on
`http://localhost:5079/bowire` once both processes are up.

## Why no .NET server

Socket.IO is a Node.js-native protocol. There is no officially-maintained
.NET server port. Real Socket.IO deployments run on Node (`socket.io`) or
Go (`go-socket.io`). `Kuestenlogik.Bowire.Protocol.SocketIo` is therefore
a **client** that connects *to* such a server.

The C# project in this folder is a no-op placeholder so the samples
solution still builds — the actual sample is `server.js`.

## Run the sample

```bash
# 1. Install dependencies (first time only)
npm install

# 2. Start the Socket.IO server (listens on ws://localhost:3000)
node server.js

# 3. In another terminal, start Bowire pointed at it
bowire --url http://localhost:3000
```

Bowire opens at <http://localhost:5079/bowire> and lists the
`/harbor` namespace's events as subscribable methods.

## What the server demonstrates

- **Namespace** `/harbor` — isolated event space
- **Rooms** `dock-{n}` — broadcast to a subset of connected clients
- **Ack callbacks** — `join-dock` and `radio` return success to the caller
- **Server-broadcast events** — `port-call-changed` every 3 seconds with a
  randomised port-call status (Scheduled / Approaching / Docked / Departing
  / Completed) — the same `HarborStore` shape as the other harbour samples

## Minimum viable Bowire setup

```csharp
builder.Services.AddBowire();     // Socket.IO plugin auto-registers
app.MapBowire();
// The plugin's settings panel lets the user configure server URL +
// namespace at runtime.
```

## Custom port

`PORT=4000 node server.js` to override the default `3000`.
