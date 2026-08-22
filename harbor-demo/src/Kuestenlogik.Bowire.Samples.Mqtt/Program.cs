// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Shared;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

// Isolated MQTT sample. Runs an embedded broker (port 1883) and a
// publisher that emits crane telemetry on
// `harbor/crane/{id}/status`. Discovery is external — browse the
// broker with a standalone Bowire:
//   bowire --url mqtt://localhost:1883

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(HarborStore.CreateSeeded());

var app = builder.Build();

var store = app.Services.GetRequiredService<HarborStore>();
var brokerFactory = new MqttServerFactory();

// ---------- Embedded broker ----------
// Port 1883 is the classic non-TLS MQTT port. For a production setup
// you'd bind 8883 with TLS or 8083 for MQTT-over-WebSocket.
var broker = brokerFactory.CreateMqttServer(
    new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithDefaultEndpointPort(1883)
        .Build());

await broker.StartAsync();

// ---------- Publisher client ----------
// Loops forever, publishes one retained status message per crane every
// second. A fresh subscriber gets the latest status immediately
// because of the retain flag.
_ = Task.Run(async () =>
{
    var clientFactory = new MqttClientFactory();
    using var pub = clientFactory.CreateMqttClient();
    await pub.ConnectAsync(new MqttClientOptionsBuilder()
        .WithClientId("harbor-publisher")
        .WithTcpServer("localhost", 1883)
        // Last Will and Testament — if our connection drops uncleanly,
        // the broker announces on `harbor/status/publisher` that we
        // went offline.
        .WithWillTopic("harbor/status/publisher")
        .WithWillPayload(Encoding.UTF8.GetBytes("{\"state\":\"offline\"}"))
        .WithWillRetain()
        .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .Build());

    var rng = new Random();
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        foreach (var crane in store.Cranes.Values)
        {
            var payload = JsonSerializer.Serialize(new
            {
                craneId = crane.Id,
                // Same shape as the Telemetry sample's frames on purpose — this
                // is the same event, and a standalone sample that describes it
                // differently teaches the wrong wire format.
                containerId = crane.LiftingContainerId,
                status = crane.Status.ToString(),
                boomAngle = Math.Round(rng.NextDouble() * 90, 1),
                loadTonnes = Math.Round(rng.NextDouble() * (double)crane.MaxLiftTonnes, 1),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            await pub.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"harbor/crane/{crane.Id}/status")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce) // QoS 1
                .WithRetainFlag()  // <-- new subscribers get latest value instantly
                .Build());
        }
        await Task.Delay(1000, app.Lifetime.ApplicationStopping);
    }
});

app.Lifetime.ApplicationStopping.Register(() => { _ = broker.StopAsync(); });

app.Run();
