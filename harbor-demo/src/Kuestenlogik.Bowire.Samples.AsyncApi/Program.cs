// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

// AsyncAPI sample — serves a schema, not a wire.
//
// AsyncAPI is the OpenAPI analogue for event-driven APIs: a YAML/JSON
// document that describes channels, operations, messages, and per-wire
// bindings. It doesn't *carry* messages itself — the wire underneath
// does. This sample therefore doesn't open any sockets; all it does is
// publish `harbor-events.asyncapi.yaml` at
//   https://localhost:5120/asyncapi.yaml
// and point the reader at the sibling Mqtt sample (port 1883) for the
// actual broker.
//
// Discovery flow from a standalone Bowire:
//   1. Start the Mqtt sample first    (https://localhost:5117 + broker on 1883)
//   2. Start this sample              (https://localhost:5120)
//   3. bowire --url https://localhost:5120/asyncapi.yaml
// Bowire's AsyncAPI plugin reads the doc, walks the channels +
// operations, and (Phase A3+) routes invocations against MQTT through
// its existing MQTT plugin.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Static content middleware would also work, but a tiny dedicated route
// is honest about what this sample exposes — exactly one document.
app.MapGet("/asyncapi.yaml", async (HttpContext ctx) =>
{
    var path = Path.Combine(app.Environment.ContentRootPath, "harbor-events.asyncapi.yaml");
    ctx.Response.ContentType = "application/yaml; charset=utf-8";
    await ctx.Response.SendFileAsync(path);
});

// Friendly root so a browser visit doesn't show a 404. Also tells the
// reader where the actual doc lives and how to point Bowire at it.
app.MapGet("/", () => Results.Text("""
    Bowire AsyncAPI sample

    Document:  /asyncapi.yaml
    Run with:  bowire --url https://localhost:5120/asyncapi.yaml
    Broker:    sibling Mqtt sample on mqtt://localhost:1883
    """, "text/plain; charset=utf-8"));

app.Run();
