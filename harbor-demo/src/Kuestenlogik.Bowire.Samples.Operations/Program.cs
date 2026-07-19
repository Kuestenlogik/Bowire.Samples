// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Operations;

// Operations — the operator console *egress* over SignalR. Consumes Tracking's
// raw AIS ingress and re-emits it with the framework-rich SignalR feature set
// (per-ship groups, broadcast, server-streaming) — the deliberate WS-vs-SignalR
// contrast from REDESIGN.md. A pure server; the Harbor.Gateway discovers it via
// the catalogue, or point a standalone workbench at signalr@http://localhost:5155/ops.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<PositionFeed>();
builder.Services.AddHostedService<TrackingConsumer>();

var app = builder.Build();

app.MapHub<OpsHub>("/ops");
app.MapGet("/", () =>
    "Operations — operator console egress over SignalR at /ops " +
    "(WatchShip / Broadcast / StreamPositions). Consumes Tracking's AIS ingress.");

app.Run();
