// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Fleet.Services;

// Fleet — the vessel-registry microservice of the harbor landscape. A pure
// gRPC server (no embedded Bowire); the Harbor.Gateway discovers it via the
// catalogue, or point a standalone workbench at grpc@http://localhost:5150.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<FleetServiceImpl>();
// Reflection so any tool (Bowire, grpcurl) can discover the service over a
// plain grpc@http:// URL without the bundled descriptor.
app.MapGrpcReflectionService();

app.MapGet("/", () =>
    "Fleet — vessel registry (gRPC, HTTP/2 on :5150). Methods: GetShip, ListShips. " +
    "Discover it from the Harbor.Gateway at /bowire, or standalone: bowire --url grpc@http://localhost:5150");

app.Run();
