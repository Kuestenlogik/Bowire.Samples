// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.TacticalApi.RadarSweep.Services;

var builder = WebApplication.CreateBuilder(args);

// gRPC service host. The canonical TacticalAPI demo runs HTTP/2 only
// — gRPC-Web stays out of the smallest-runnable-target scope so the
// listener wire is as close as possible to how a real C4I component
// would talk to the plugin.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Single shared in-memory backend. Singleton so the mover task lives
// for the host's lifetime and SubscribeSituationObjectEvents
// subscribers see broadcasts from the same store every GetSituationObjects
// snapshot uses.
builder.Services.AddSingleton<SituationServiceImpl>();

var app = builder.Build();

// Wire the same singleton instance to every gRPC method on the
// Situation service.
app.MapGrpcService<SituationServiceImpl>();

// gRPC server reflection — even though the TacticalAPI plugin bundles
// its own .proto for discovery, reflection costs nothing extra and
// lets tools that DO walk reflection (grpcurl, postman) find the
// service without the bundled schema.
app.MapGrpcReflectionService();

app.MapGet("/", () =>
    "TacticalApi.RadarSweep — three radar tracks circling around 54°N 11.5°E. " +
    "Connect Bowire via `bowire --url http://localhost:5191` and pick the " +
    "Situation service to see GetSituationObjects + SubscribeSituationObjectEvents.");

app.Run();
