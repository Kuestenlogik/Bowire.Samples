// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

// Socket.IO has no officially-supported .NET server implementation —
// real Socket.IO deployments run on Node.js or Go. The
// Kuestenlogik.Bowire.Protocol.SocketIo plugin is therefore a CLIENT that
// connects to a Node-hosted server.
//
// This project has no runtime code: it exists so the solution keeps
// a pointer to the Node.js server recipe in README.md and so
// `dotnet build` on the samples solution doesn't have to special-case
// Socket.IO. Browse a Socket.IO server with a standalone Bowire:
//   bowire --url http://localhost:3000

Console.WriteLine("Kuestenlogik.Bowire.Samples.SocketIo — see README.md for the Node.js server recipe.");
Console.WriteLine("Then browse with: bowire --url http://localhost:3000");
