// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Samples.Shared;
using RabbitMQ.Client;

// Force the AMQP plugin assembly to load before AddBowire's
// reflection scan runs — without an explicit type reference the JIT
// only loads the plugin DLL on first use, too late for the
// discovery pass.
_ = typeof(Kuestenlogik.Bowire.Protocol.Amqp.BowireAmqpProtocol);

// Isolated AMQP 0.9.1 sample. Expects a RabbitMQ broker on
// amqp://localhost:5672 (see compose.yaml next to the project). The
// host runs the Bowire workbench on /bowire (pre-seeded with the
// broker URL so the sidebar populates on page load) plus a publisher
// that emits crane telemetry to the `harbor` topic exchange every
// second.
//
// Open https://localhost:5118/bowire and pick the AMQP tab to
// observe the live stream — no extra connect dialog needed.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());
builder.Services.AddBowire();

var app = builder.Build();

// Pre-seed the broker as a discovered ServerUrl so the workbench
// shows the harbor exchange + crane routing keys in the sidebar
// immediately after the page loads.
app.MapBowire(options =>
{
    options.ServerUrls.Add("amqp://localhost:5672");
});

var store = app.Services.GetRequiredService<HarborStore>();
var lifetime = app.Lifetime;

// ---------- Publisher ----------
// Runs in the background, opens a persistent connection + channel
// against the RabbitMQ broker, and publishes one retained-style status
// per crane per second to `harbor` with routing key
// `crane.{id}.status`. AMQP 0.9.1 doesn't have MQTT-style retain; we
// approximate it with persistent delivery so the broker keeps the last
// message in its queue across consumer-restart cycles.
_ = Task.Run(async () =>
{
    var factory = new ConnectionFactory
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest",
        // Wait for the broker to come up — RabbitMQ in Docker takes a
        // few seconds to finish boot before AMQP listens.
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(2),
    };

    IConnection? conn = null;
    for (var i = 0; i < 20 && !lifetime.ApplicationStopping.IsCancellationRequested; i++)
    {
        try
        {
            conn = await factory.CreateConnectionAsync(lifetime.ApplicationStopping);
            break;
        }
        catch
        {
            await Task.Delay(TimeSpan.FromSeconds(2), lifetime.ApplicationStopping);
        }
    }
    if (conn is null)
    {
        Console.Error.WriteLine("[amqp-sample] Could not reach the broker at localhost:5672. Is `docker compose up rabbitmq` running?");
        return;
    }

    using (conn)
    {
        await using var channel = await conn.CreateChannelAsync();

        // Topic exchange means routing keys like `crane.7.status` can
        // be subscribed with wildcards (`crane.*.status`, `crane.#`).
        await channel.ExchangeDeclareAsync(
            exchange: "harbor",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        // Declare + bind a queue so Bowire's discovery surfaces a
        // `receive` method for the wildcard. Without a bound queue
        // the broker's topology is empty from the consumer side
        // (publishers don't create queues in AMQP 0.9.1) and the
        // workbench has no consume endpoint to show in the sidebar.
        await channel.QueueDeclareAsync(
            queue: "harbor.cranes",
            durable: true,
            exclusive: false,
            autoDelete: false);
        await channel.QueueBindAsync(
            queue: "harbor.cranes",
            exchange: "harbor",
            routingKey: "crane.*.status");

        var rng = new Random();
        var props = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
        };

        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            foreach (var crane in store.Cranes.Values)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    craneId = crane.Id,
                    status = crane.Status.ToString(),
                    boomAngle = Math.Round(rng.NextDouble() * 90, 1),
                    loadTonnes = Math.Round(rng.NextDouble() * (double)crane.MaxLiftTonnes, 1),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });

                await channel.BasicPublishAsync(
                    exchange: "harbor",
                    routingKey: $"crane.{crane.Id}.status",
                    mandatory: false,
                    basicProperties: props,
                    body: payload,
                    cancellationToken: lifetime.ApplicationStopping);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), lifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
});

app.Run();
