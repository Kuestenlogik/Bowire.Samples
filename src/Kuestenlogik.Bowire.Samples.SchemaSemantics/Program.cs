// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.SchemaSemantics.Services;

// Standalone gRPC server demonstrating Bowire's frame-semantics
// framework end-to-end without any Bowire-specific code in the sample
// itself. The whole point is that this server is plain gRPC — no
// IBowireSchemaHints, no annotations file, no plugin awareness — and
// Bowire still mounts the map widget against it because the field
// names + value ranges match the WGS84 coordinate detector.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

// Dual transport so a Bowire client can connect via either
// `grpc@https://localhost:5111` (native HTTP/2) or
// `grpcweb@https://localhost:5111` (HTTP/1.1) — the same toggle that
// Rheinmetall's TacticalAPI exposes on ports 4267 / 4268.
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.MapGrpcService<ShipsService>();
app.MapGrpcReflectionService();

app.Run();
