// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Protocol.Sse;

// Isolated SSE sample — a one-way stream of port-call status changes
// plus an arrivals feed for the kitchen-display pattern. Each frame
// carries an `id:` so a client that reconnects can resume from the
// last-seen event (Last-Event-ID header).

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddBowire();

var app = builder.Build();

// Monotonic event counter shared by every stream so Last-Event-ID
// values are comparable across reconnects.
long nextEventId = 0;
var eventHistory = new ConcurrentQueue<(long Id, string Type, string Json)>();

// Replay broadcaster — every port-call change becomes an event frame.
var store = app.Services.GetRequiredService<HarborStore>();
store.PortCallChanged += pc =>
{
    var id = Interlocked.Increment(ref nextEventId);
    var json = JsonSerializer.Serialize(pc);
    eventHistory.Enqueue((id, "port-call-changed", json));

    // Keep the buffer bounded — 512 events is generous for a demo.
    while (eventHistory.Count > 512 && eventHistory.TryDequeue(out _)) { }
};

// --------------------------------------------------------------
// /events/port-calls — status changes
// --------------------------------------------------------------
app.MapGet("/events/port-calls", async (HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("X-Accel-Buffering", "no"); // don't let nginx buffer

    // If the client sends a Last-Event-ID header, replay events that
    // happened after that id before going live. This is the contract
    // EventSource clients expect for automatic reconnection.
    long since = 0;
    if (ctx.Request.Headers.TryGetValue("Last-Event-ID", out var lastHeader) &&
        long.TryParse(lastHeader.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
    {
        since = parsed;
    }

    foreach (var (id, type, json) in eventHistory.Where(e => e.Id > since))
    {
        await WriteEvent(ctx, id, type, json);
    }

    // Then switch to live-follow. Subscribe to the in-memory event;
    // a real implementation would pull from a message bus.
    var queue = System.Threading.Channels.Channel.CreateUnbounded<(long, string, string)>();
    void Handler(PortCall pc)
    {
        var id = Interlocked.Increment(ref nextEventId);
        queue.Writer.TryWrite((id, "port-call-changed", JsonSerializer.Serialize(pc)));
    }
    store.PortCallChanged += Handler;

    try
    {
        await foreach (var (id, type, json) in queue.Reader.ReadAllAsync(ctx.RequestAborted))
            await WriteEvent(ctx, id, type, json);
    }
    catch (OperationCanceledException) { /* client disconnected */ }
    finally { store.PortCallChanged -= Handler; }

    static async Task WriteEvent(HttpContext ctx, long id, string type, string data)
    {
        await ctx.Response.WriteAsync($"id: {id}\nevent: {type}\ndata: {data}\n\n", ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
}).WithMetadata(new SseEndpointAttribute { Description = "Port-call status changes (replayable via Last-Event-ID)", EventType = "port-call-changed" });

// --------------------------------------------------------------
// /events/heartbeat — a slow tick the client can use to verify the
// connection is alive when port-calls go quiet.
// --------------------------------------------------------------
app.MapGet("/events/heartbeat", async (HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");

    long id = 0;
    while (!ctx.RequestAborted.IsCancellationRequested)
    {
        id++;
        await ctx.Response.WriteAsync($"id: {id}\nevent: heartbeat\ndata: {{\"at\": \"{DateTimeOffset.UtcNow:o}\"}}\n\n", ctx.RequestAborted);
        try { await ctx.Response.Body.FlushAsync(ctx.RequestAborted); }
        catch { return; }
        try { await Task.Delay(5000, ctx.RequestAborted); }
        catch { return; }
    }
}).WithMetadata(new SseEndpointAttribute { Description = "Slow keep-alive tick so clients can detect dead connections", EventType = "heartbeat" });

app.MapBowire();
app.Run();
