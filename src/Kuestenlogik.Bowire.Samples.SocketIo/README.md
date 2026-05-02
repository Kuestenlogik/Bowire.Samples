# Kuestenlogik.Bowire.Samples.SocketIo

Isolated **Socket.IO** sample. Bowire UI on `https://localhost:5118/bowire`.

## Why there's no .NET server here

Socket.IO is a Node.js-native protocol. There is no officially-maintained .NET server port. Real Socket.IO deployments run on Node (`socket.io` package) or Go (`go-socket.io`). `Kuestenlogik.Bowire.Protocol.SocketIo` is therefore a **client** that connects *to* such a server.

This sample runs only the Bowire UI shell. To demo the full flow, run the bundled Node server alongside (or point the plugin at any existing Socket.IO endpoint).

## Minimal Node.js server

Save as `server.js` next to this README:

```javascript
// npm install socket.io@4
const { Server } = require("socket.io");
const io = new Server(3000, { cors: { origin: "*" } });

// Namespace + rooms per dock
const harbor = io.of("/harbor");
harbor.on("connection", (socket) => {
    console.log("harbor connected:", socket.id);

    socket.on("join-dock", (dockNumber, ack) => {
        socket.join(`dock-${dockNumber}`);
        ack({ ok: true, room: `dock-${dockNumber}` });
    });

    socket.on("radio", (msg, ack) => {
        harbor.to(`dock-${msg.dockNumber}`).emit("radio", {
            from: socket.id, text: msg.text, at: new Date().toISOString()
        });
        if (ack) ack({ delivered: true });
    });
});

// Broadcast a fake port-call status every 3s
setInterval(() => {
    harbor.emit("port-call-changed", {
        id: Math.floor(Math.random() * 1000),
        status: ["Scheduled","Approaching","Docked","Departing"][Math.floor(Math.random()*4)],
        at: new Date().toISOString()
    });
}, 3000);
```

Run:

```bash
npm install socket.io@4
node server.js     # listening on ws://localhost:3000
```

Then add `ws://localhost:3000` with namespace `/harbor` in the Bowire Socket.IO plugin settings.

## What the Node server demonstrates

- **Namespace** `/harbor` — isolated event space
- **Rooms** `dock-{n}` — broadcast to a subset of connected clients
- **Ack callbacks** — `join-dock` and `radio` return success to the caller
- **Server-broadcast events** — `port-call-changed` every 3 seconds

## Minimum viable Bowire setup

```csharp
builder.Services.AddBowire();     // Socket.IO plugin auto-registers
app.MapBowire();
// Bowire UI lets the user add the Socket.IO server URL + namespace
// through the plugin settings panel.
```
