// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Protocol.WebSocket;

// Isolated WebSocket sample: two endpoints, one for text frames, one
// for binary frames, plus sub-protocol negotiation and a periodic
// keep-alive ping.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddBowire();

var app = builder.Build();

// KeepAliveInterval triggers ping frames when the client has been
// quiet — useful on NAT paths that drop idle sockets after a minute.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// --------------------------------------------------------------
// Text frames: live ship tracker (AIS-style)
// --------------------------------------------------------------
// Sub-protocol: `bowire-ais.v1`. Client selects it via the
// Sec-WebSocket-Protocol header; server must echo it back or the
// handshake fails. Bowire's WebSocket request pane lets you set it.
app.MapGet("/ws/ship-tracker", async (HttpContext ctx, HarborStore store) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

    var requested = ctx.WebSockets.WebSocketRequestedProtocols;
    string? accepted = requested.Contains("bowire-ais.v1") ? "bowire-ais.v1" : null;

    using var socket = await ctx.WebSockets.AcceptWebSocketAsync(accepted);
    var rng = new Random();
    var positions = store.Ships.Values.ToDictionary(
        s => s.Id,
        s => (Lat: 53.5 + rng.NextDouble() * 0.3, Lon: 9.8 + rng.NextDouble() * 0.3));

    while (socket.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
    {
        foreach (var kvp in positions.ToList())
        {
            var next = (
                Lat: kvp.Value.Lat + (rng.NextDouble() - 0.5) * 0.002,
                Lon: kvp.Value.Lon + (rng.NextDouble() - 0.5) * 0.002);
            positions[kvp.Key] = next;

            var json = JsonSerializer.Serialize(new
            {
                shipId = kvp.Key,
                name = store.Ships[kvp.Key].Name,
                lat = Math.Round(next.Lat, 5),
                lon = Math.Round(next.Lon, 5),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            try { await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ctx.RequestAborted); }
            catch { return; }
        }
        try { await Task.Delay(1000, ctx.RequestAborted); }
        catch { return; }
    }
}).WithMetadata(new WebSocketEndpointAttribute("Ship tracker", "Live AIS-style ship positions, text frames"));

// --------------------------------------------------------------
// Binary frames: manifest upload
// --------------------------------------------------------------
// Client pushes raw bytes (e.g. a CSV blob). Server accumulates until
// the client signals "end of message" or closes. A text frame is sent
// back with the upload summary.
app.MapGet("/ws/manifest-upload", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[8 * 1024];
    using var stream = new MemoryStream();

    while (socket.State == WebSocketState.Open)
    {
        WebSocketReceiveResult result;
        try { result = await socket.ReceiveAsync(buffer, ctx.RequestAborted); }
        catch { break; }

        if (result.MessageType == WebSocketMessageType.Close) break;
        if (result.MessageType != WebSocketMessageType.Binary)
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                "expected binary frames", ctx.RequestAborted);
            return;
        }

        stream.Write(buffer, 0, result.Count);

        if (result.EndOfMessage)
        {
            var summary = JsonSerializer.Serialize(new
            {
                bytesReceived = stream.Length,
                firstByte = stream.Length > 0 ? (int)stream.GetBuffer()[0] : -1
            });
            await socket.SendAsync(Encoding.UTF8.GetBytes(summary), WebSocketMessageType.Text, true, ctx.RequestAborted);
            stream.SetLength(0);
        }
    }
}).WithMetadata(new WebSocketEndpointAttribute("Manifest upload", "Accumulates binary frames until EndOfMessage, then acks with a text summary"));

app.MapBowire();
app.Run();
