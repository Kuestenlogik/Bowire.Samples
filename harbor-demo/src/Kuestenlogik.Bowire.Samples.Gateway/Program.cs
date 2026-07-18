// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Sources;

// Harbor.Gateway — the one Bowire workbench over the whole harbor landscape.
// It hosts no domain of its own; it just mounts Bowire and points its
// catalogue provider at a local harbor-catalogue.json listing every harbor
// microservice + the protocol it speaks. Open /bowire and the Sources rail
// shows Fleet (gRPC), Inventory (OData), … — discovered across their wires.
//
// The browser connects directly to each service's origin (no reverse proxy).
// Each service can also be studied standalone with `bowire --url <service>`.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBowire();
builder.Services.AddBowireCatalogue(builder.Configuration);

var app = builder.Build();

app.MapBowire();
app.MapGet("/", () => Results.Redirect("/bowire"));

app.Run();
