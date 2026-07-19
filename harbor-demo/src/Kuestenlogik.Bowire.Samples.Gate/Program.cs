// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Gate;
using Kuestenlogik.Bowire.Samples.Shared;

// Gate — container gate-in/out over REST / OpenAPI. Owns the Container
// lifecycle in its own private store. A pure server; the Harbor.Gateway
// discovers it via the catalogue, or point a standalone workbench at
// rest@http://localhost:5152 (spec at /openapi/v1.json).

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GateStore>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.MapOpenApi();

app.MapGet("/containers", (GateStore store, int? onShipId) =>
        onShipId is null ? store.All : store.All.Where(c => c.OnShipId == onShipId))
    .WithName("ListContainers").WithTags("Containers");

app.MapGet("/containers/{id}", (GateStore store, string id) =>
        store.Find(id) is { } c
            ? Results.Ok(c)
            : Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Container not found", detail: $"No container '{id}'."))
    .WithName("GetContainer").WithTags("Containers");

// Gate-in: register a new container in the yard. 409 if the id already exists.
app.MapPost("/containers", (GateStore store, Container input) =>
    {
        if (store.Exists(input.Id))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Container already gated in", detail: $"'{input.Id}' already exists.");
        var c = input with { Status = ContainerStatus.Stored, OnShipId = null };
        store.Put(c);
        return Results.Created($"/containers/{c.Id}", c);
    })
    .WithName("GateInContainer").WithTags("Containers");

// Load a container onto a ship (references Fleet's ShipId by value).
app.MapPost("/containers/{id}/load", (GateStore store, string id, int shipId) =>
    {
        if (store.Find(id) is not { } c)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Container not found", detail: $"No container '{id}'.");
        var loaded = c with { Status = ContainerStatus.Loading, OnShipId = shipId };
        store.Put(loaded);
        return Results.Ok(loaded);
    })
    .WithName("LoadContainer").WithTags("Containers");

// Discharge a container back to the yard.
app.MapPost("/containers/{id}/discharge", (GateStore store, string id) =>
    {
        if (store.Find(id) is not { } c)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Container not found", detail: $"No container '{id}'.");
        var discharged = c with { Status = ContainerStatus.Stored, OnShipId = null };
        store.Put(discharged);
        return Results.Ok(discharged);
    })
    .WithName("DischargeContainer").WithTags("Containers");

// Gate-out: remove a container from the yard.
app.MapDelete("/containers/{id}", (GateStore store, string id) =>
        store.Remove(id)
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Container not found", detail: $"No container '{id}'."))
    .WithName("GateOutContainer").WithTags("Containers");

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"));

app.Run();
