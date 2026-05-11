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

// Enable gRPC-Web on every mapped gRPC service so the same HarborService
// is callable via the native HTTP/2 transport AND the gRPC-Web HTTP/1.1
// transport — Bowire 1.2.0+ can switch between them by prefixing the URL
// with `grpc@` (default) or `grpcweb@`. CORS isn't enabled here because
// Bowire's own workbench talks server-to-server; flip on `app.UseCors()`
// before `UseGrpcWeb` if you want browsers to consume the web endpoint.
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.MapGrpcService<HarborGrpcService>();
app.MapBowire();

app.Run();
