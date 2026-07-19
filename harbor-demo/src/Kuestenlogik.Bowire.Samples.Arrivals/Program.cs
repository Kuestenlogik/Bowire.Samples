// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Threading.Channels;
using Kuestenlogik.Bowire.Samples.Arrivals;
using Kuestenlogik.Bowire.Samples.Shared;

// Arrivals — the public arrivals board as a CQRS read-model over SSE. The
// write side is PortCalls' GraphQL mutations; this is the read projection,
// pushed as Server-Sent Events with a bounded replay buffer so Last-Event-ID
// resume works. A pure server; the Harbor.Gateway discovers it via the
// catalogue, or point a standalone workbench at sse@http://localhost:5156/arrivals.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ArrivalsFeed>();
builder.Services.AddHostedService<ArrivalsProjector>();

var app = builder.Build();

app.MapGet("/arrivals", async (HttpContext ctx, ArrivalsFeed feed) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    var ct = ctx.RequestAborted;

    // Resume point: the SSE spec sends the last id the client saw back as the
    // Last-Event-ID request header on reconnect.
    long lastSeq = 0;
    if (long.TryParse(ctx.Request.Headers["Last-Event-ID"], out var id))
        lastSeq = id;

    var channel = Channel.CreateUnbounded<ArrivalEvent>();
    void Handler(ArrivalEvent e) => channel.Writer.TryWrite(e);
    feed.OnEvent += Handler;   // subscribe first so nothing between replay + live is dropped
    try
    {
        // Replay the bounded buffer from the resume point…
        foreach (var e in feed.Since(lastSeq))
        {
            await WriteEvent(ctx.Response, e, ct);
            lastSeq = e.Seq;
        }
        // …then stream live, skipping anything the replay already covered.
        await foreach (var e in channel.Reader.ReadAllAsync(ct))
        {
            if (e.Seq <= lastSeq) continue;
            await WriteEvent(ctx.Response, e, ct);
            lastSeq = e.Seq;
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected — expected.
    }
    finally
    {
        feed.OnEvent -= Handler;
    }
});

app.MapGet("/", () =>
    "Arrivals — public arrivals board (CQRS read-model) over SSE at /arrivals, " +
    "resumable via Last-Event-ID.");

app.Run();

static async Task WriteEvent(HttpResponse response, ArrivalEvent e, CancellationToken ct)
{
    var data = JsonSerializer.Serialize(e);
    await response.WriteAsync($"id: {e.Seq}\nevent: arrival\ndata: {data}\n\n", ct);
    await response.Body.FlushAsync(ct);
}
