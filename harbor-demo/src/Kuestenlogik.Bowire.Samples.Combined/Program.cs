// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Combined.Hubs;
using Kuestenlogik.Bowire.Samples.Combined.Services;
using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Protocol.WebSocket;

var builder = WebApplication.CreateBuilder(args);

// ---- Harbor state ----
// One in-memory store shared by every surface (gRPC, REST, SignalR, SSE,
// WebSocket). All samples boot with the same three ships / five docks /
// two active port calls so screenshots are deterministic.
builder.Services.AddSingleton(HarborStore.CreateSeeded());

// ---- Protocol hosts ----
builder.Services.AddGrpc();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---- Bowire ----
// AddBowire auto-registers prerequisites for every installed plugin
// (AddGrpcReflection for Grpc, metadata scanners for SignalR / REST,
// etc.). Every plugin listed in the csproj wires itself in.
builder.Services.AddBowire();

var app = builder.Build();

app.MapOpenApi();
app.UseWebSockets();

// ---- gRPC ----
app.MapGrpcService<HarborGrpcService>();

// ---- REST ----
app.MapControllers();

// ---- SignalR ----
app.MapHub<PortCallHub>("/hubs/port-calls");

// ---- WebSocket — live ship tracker ----
// An AIS-inspired broadcast: every connected client receives a JSON frame
// per known ship with a wobbling lat/lon every second. One-way for
// simplicity — clients don't send anything back.
app.MapGet("/ws/ship-tracker", async (HttpContext ctx, HarborStore store) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
    var rng = new Random();

    // Seed each ship with a plausible North-Sea position; then wobble it.
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

            var payload = JsonSerializer.Serialize(new
            {
                shipId = kvp.Key,
                name = store.Ships[kvp.Key].Name,
                lat = Math.Round(next.Lat, 5),
                lon = Math.Round(next.Lon, 5),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            var bytes = Encoding.UTF8.GetBytes(payload);
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ctx.RequestAborted);
            }
            catch { return; }
        }
        try { await Task.Delay(1000, ctx.RequestAborted); }
        catch { return; }
    }
});

// ---- SSE — kitchen-display-style arrivals feed ----
// One-way stream of port-call status changes. Matches the same events
// the SignalR hub emits, just over the server-sent-events protocol so
// browsers can consume it with a plain EventSource.
app.MapGet("/events/port-calls", async (HttpContext ctx, HarborStore store) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");

    var queue = System.Threading.Channels.Channel.CreateUnbounded<PortCall>();
    void Handler(PortCall pc) => queue.Writer.TryWrite(pc);
    store.PortCallChanged += Handler;

    try
    {
        await foreach (var pc in queue.Reader.ReadAllAsync(ctx.RequestAborted))
        {
            var json = JsonSerializer.Serialize(pc);
            await ctx.Response.WriteAsync($"event: port-call\ndata: {json}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client left */ }
    finally { store.PortCallChanged -= Handler; }
});

// ---- Bowire UI mounted last at /bowire ----
app.MapBowire();

app.Run();
