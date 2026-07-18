// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Interceptor;
using Kuestenlogik.Bowire.Samples.Shared;

// Interceptor sample — Bowire embedded in a host with the transparent
// in-process interceptor turned on. Every request through this host
// (the seeded Harbor REST endpoints below, or anything a client hits)
// is tee'd into the workbench's "Intercepted" rail: method, path,
// headers, request + response body, status, latency. No client-side
// setup, no cert trust, no separate proxy process. This is the one
// embedding facet the other harbor-demo samples don't show.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// AddBowire wires the workbench + the Rest plugin (from the csproj) so
// the host's own endpoints show up in Discover alongside the
// intercepted traffic.
builder.Services.AddBowire();

var app = builder.Build();

// #153 — Transparent in-process interceptor. Tees every request into
// the workbench's "Intercepted" rail; the workbench's own /bowire/*
// surface is excluded by default so the rail doesn't observe itself.
// When a recording is open, intercepted flows also auto-append as steps.
app.UseBowireInterceptor();

// OpenAPI document at /openapi/v1.json — the Rest plugin reads it to
// enumerate the host's own paths into the Discover tree.
app.MapOpenApi();

// Seeded Harbor endpoints — real host traffic for the interceptor to
// capture and for the Rest plugin to discover.
app.MapGet("/api/ships", (HarborStore s) => s.Ships.Values)
    .WithName("ListShips").WithTags("Ships");

app.MapGet("/api/ships/{id:int}", (int id, HarborStore s) =>
    s.Ships.TryGetValue(id, out var ship)
        ? Results.Ok(ship)
        : Results.NotFound(new { error = $"ship {id} not found" }))
    .WithName("GetShip").WithTags("Ships");

app.MapGet("/api/docks", (HarborStore s) => s.Docks.Values)
    .WithName("ListDocks").WithTags("Docks");

app.MapGet("/api/port-calls", (HarborStore s) => s.PortCalls.Values)
    .WithName("ListPortCalls").WithTags("PortCalls");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", server = "interceptor-sample" }))
    .WithName("Health").WithTags("Ops");

// Bowire workbench mounted at /bowire — embedded-mode convention.
app.MapBowire("/bowire");

// Root redirect so a curious operator hitting / lands somewhere useful.
app.MapGet("/", () => Results.Redirect("/bowire"));

app.Run();
