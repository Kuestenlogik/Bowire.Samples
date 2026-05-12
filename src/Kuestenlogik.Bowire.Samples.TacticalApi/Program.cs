// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Samples.TacticalApi.Services;

// Isolated TacticalAPI sample. Hosts Rheinmetall's `Situation` service over
// both native HTTP/2 gRPC and gRPC-Web (HTTP/1.1) so the Bowire workbench
// can demo the typed-discovery + invoke story against TacticalAPI without a
// real C4I backend. Seeded with three MIL-2525C symbols along the German
// North Sea coast — see SeededSituation.cs.
//
// Bowire UI is mounted at /bowire so the same process serves both the gRPC
// surface (for `bowire --url tacticalapi@http://localhost:5120`) and the
// in-page exploration UI at http://localhost:5120/bowire.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddBowire();

// No gRPC reflection: the TacticalAPI plugin's whole point is bundled-
// descriptor discovery, so the sample wants the workbench to enter the
// typed path (tacticalapi@…) rather than the reflection one (grpc@…).
// Skipping reflection also keeps the discovery tree from doubling up
// when both plugins are active in the same bowire process.

var app = builder.Build();

// gRPC-Web wrapping enabled on every mapped service: the same Situation
// service is reachable via tacticalapi@http://localhost:5120 (HTTP/2) and
// via grpcweb@http://localhost:5120 (HTTP/1.1) — mirrors Rheinmetall TacNet's
// dual-port (4267 / 4268) convention documented upstream.
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.MapGrpcService<SituationServiceImpl>();
app.MapBowire();

app.Run();
