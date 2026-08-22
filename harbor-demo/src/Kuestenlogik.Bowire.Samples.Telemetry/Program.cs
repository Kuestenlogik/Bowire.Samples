// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

// Telemetry — the equipment device bus. Owns both the wire (an embedded MQTT
// broker on :1883) AND its schema (an AsyncAPI doc at :5157/asyncapi.yaml), so
// Bowire can discover it two ways: topic-tree via mqtt://localhost:1883, or
// schema-driven via the AsyncAPI document.
//
// This is the live half of the Crane shared-kernel split from REDESIGN.md:
// Inventory (OData) owns each crane's static config keyed by CraneId;
// Telemetry owns the live status on the same CraneId.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Crane ids/config come from the shared seed — same CraneId Inventory serves.
var cranes = HarborSeed.Cranes();

// ---------- Embedded MQTT broker on :1883 ----------
var brokerFactory = new MqttServerFactory();
var broker = brokerFactory.CreateMqttServer(
    new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithDefaultEndpointPort(1883)
        .Build());
await broker.StartAsync();
app.Lifetime.ApplicationStopping.Register(() => { _ = broker.StopAsync(); });

// ---------- Publisher: one retained status frame per crane, every second ----------
_ = Task.Run(async () =>
{
    var clientFactory = new MqttClientFactory();
    using var pub = clientFactory.CreateMqttClient();
    await pub.ConnectAsync(new MqttClientOptionsBuilder()
        .WithClientId("harbor-telemetry")
        .WithTcpServer("localhost", 1883)
        .WithWillTopic("harbor/status/telemetry")
        .WithWillPayload(Encoding.UTF8.GetBytes("{\"state\":\"offline\"}"))
        .WithWillRetain()
        .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build());

    var rng = new Random();
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        foreach (var crane in cranes)
        {
            // containerId is what joins this stream to the rest of the demo.
            // Without it the only value shared with any other step is the
            // integer on craneId, which the correlation analyzer rejects — and
            // should: a short number appearing in two payloads is a
            // coincidence, and accepting it would fuse unrelated flows. A crane
            // that says which container it is lifting supplies real evidence
            // instead, which is what this demo exists to show.
            var payload = JsonSerializer.Serialize(new
            {
                craneId = crane.Id,
                containerId = crane.LiftingContainerId,
                status = crane.Status.ToString(),
                boomAngleDeg = Math.Round(rng.NextDouble() * 90, 1),
                loadTonnes = Math.Round(rng.NextDouble() * (double)crane.MaxLiftTonnes, 1),
                at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            await pub.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"harbor/crane/{crane.Id}/status")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag()
                .Build());
        }
        try { await Task.Delay(1000, app.Lifetime.ApplicationStopping); }
        catch (OperationCanceledException) { break; }
    }
});

// ---------- AsyncAPI schema endpoint ----------
app.MapGet("/asyncapi.yaml", async (HttpContext ctx) =>
{
    var path = Path.Combine(app.Environment.ContentRootPath, "harbor-telemetry.asyncapi.yaml");
    ctx.Response.ContentType = "application/yaml; charset=utf-8";
    await ctx.Response.SendFileAsync(path);
});

app.MapGet("/", () =>
    "Telemetry — equipment device bus. MQTT broker on mqtt://localhost:1883 " +
    "(topic harbor/crane/{id}/status); AsyncAPI schema at /asyncapi.yaml.");

app.Run();
