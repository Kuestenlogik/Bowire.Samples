// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Grpc.Services;
using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire;

// Isolated gRPC sample — one protocol, all four call types, Bowire UI
// mounted at /bowire for exploration. Use this as the copy-paste
// starter when adding Bowire to an existing gRPC service.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddGrpc();

// AddBowire picks up Kuestenlogik.Bowire.Protocol.Grpc from the csproj and wires
// up gRPC reflection automatically. The reflection registration is what
// lets the Bowire UI enumerate services and methods without a manual
// discovery step.
builder.Services.AddBowire();

var app = builder.Build();

app.MapGrpcService<HarborGrpcService>();
app.MapBowire();

app.Run();
