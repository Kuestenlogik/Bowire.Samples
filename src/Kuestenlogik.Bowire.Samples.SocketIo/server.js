// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0
//
// Minimal Node.js Socket.IO server used by Kuestenlogik.Bowire.Samples.SocketIo.
//
// Usage (from this directory):
//   npm install
//   node server.js                  # listens on ws://localhost:3000
//
// Then point Bowire at it:
//   bowire --url http://localhost:3000

const { Server } = require('socket.io');

const PORT = process.env.PORT ? Number(process.env.PORT) : 3000;
const io = new Server(PORT, { cors: { origin: '*' } });

// ---------- /harbor namespace ----------
// Per-dock rooms, ack-callback patterns, server-emitted broadcasts. The
// Bowire Socket.IO plugin lists every emitted event name as a method
// the user can subscribe to.
const harbor = io.of('/harbor');
harbor.on('connection', (socket) => {
    console.log('harbor connected:', socket.id);

    socket.on('join-dock', (dockNumber, ack) => {
        socket.join(`dock-${dockNumber}`);
        if (typeof ack === 'function') ack({ ok: true, room: `dock-${dockNumber}` });
    });

    socket.on('radio', (msg, ack) => {
        const dockNumber = msg && msg.dockNumber;
        const text = msg && msg.text;
        harbor.to(`dock-${dockNumber}`).emit('radio', {
            from: socket.id,
            text,
            at: new Date().toISOString(),
        });
        if (typeof ack === 'function') ack({ delivered: true });
    });

    socket.on('disconnect', () => {
        console.log('harbor disconnected:', socket.id);
    });
});

// Broadcast a synthetic port-call-status update every second on the
// /harbor namespace only — Socket.IO's natural pattern is one
// namespace per business domain. Bowire clients connect by either
// passing `http://localhost:3000/harbor` as the URL or setting the
// `X-Bowire-SocketIo-Namespace: /harbor` metadata header.
const STATUSES = ['Scheduled', 'Approaching', 'Docked', 'Departing', 'Completed'];
setInterval(() => {
    harbor.emit('port-call-changed', {
        id: Math.floor(Math.random() * 1000),
        shipId: 100 + Math.floor(Math.random() * 50),
        dockNumber: 1 + Math.floor(Math.random() * 6),
        status: STATUSES[Math.floor(Math.random() * STATUSES.length)],
        at: new Date().toISOString(),
    });
}, 1000);

console.log(`Bowire SocketIo sample server listening on ws://localhost:${PORT}`);
console.log('Namespace: /harbor');
console.log('Browse with: bowire --url http://localhost:' + PORT);
