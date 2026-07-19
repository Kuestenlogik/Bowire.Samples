// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire.Samples.Tracking;

// Tracking — raw AIS position *ingress* over WebSocket. The deliberately
// framework-light edge (contrast with Operations' SignalR egress): a plain
// WebSocket streaming position frames, with sub-protocol negotiation between
// `ais+json` (text frames) and `ais+nmea` (binary frames). A pure server; the
// Harbor.Gateway discovers it via the catalogue, or point a standalone
// workbench at websocket@ws://localhost:5154/ais.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PositionSimulator>();

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/ais", async (HttpContext ctx, PositionSimulator sim) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("WebSocket upgrade required at /ais.");
        return;
    }

    // Sub-protocol negotiation: raw AIS as text JSON, or NMEA-style binary.
    var requested = ctx.WebSockets.WebSocketRequestedProtocols;
    var proto = requested.Contains("ais+nmea") ? "ais+nmea"
        : requested.Contains("ais+json") ? "ais+json"
        : null;
    using var ws = proto is null
        ? await ctx.WebSockets.AcceptWebSocketAsync()
        : await ctx.WebSockets.AcceptWebSocketAsync(proto);

    var binary = proto == "ais+nmea";
    var ct = ctx.RequestAborted;

    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
    {
        foreach (var pos in sim.Snapshot())
        {
            if (binary)
            {
                var frame = Encoding.UTF8.GetBytes(ToNmea(pos));
                await ws.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct);
            }
            else
            {
                var frame = JsonSerializer.SerializeToUtf8Bytes(pos);
                await ws.SendAsync(frame, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }

        try { await Task.Delay(1000, ct); }
        catch (OperationCanceledException) { break; }
    }
});

app.MapGet("/", () =>
    "Tracking — raw AIS position ingress over WebSocket at /ais " +
    "(sub-protocols: ais+json = text frames, ais+nmea = binary frames).");

app.Run();

// A pseudo-AIVDM sentence so the binary path carries something AIS-shaped.
static string ToNmea(AisPosition p) =>
    $"!AIVDM,1,1,,A,ship{p.ShipId};lat{p.Latitude};lon{p.Longitude};" +
    $"sog{p.SpeedKnots};cog{p.CourseDegrees};t{p.At.ToUnixTimeSeconds()}*00";
