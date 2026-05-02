# Kuestenlogik.Bowire.Samples.Mqtt

Isolated **MQTT** sample. The process runs three things:

1. an **embedded MQTT broker** on port **1883**
2. a **publisher** that pushes crane telemetry every second
3. the **Bowire UI** on `https://localhost:5117/bowire`

No external broker needed — Mosquitto / Eclipse Mosca / AWS IoT Core
would all be drop-in replacements for production.

## What this demonstrates

- **Topic wildcards** — subscribe to `harbor/crane/+/status` to get every crane, `harbor/#` to get the whole branch
- **QoS 1 (AtLeastOnce)** on publishes — the broker redelivers until acked
- **Retained messages** — last crane status is kept on the broker, so a fresh subscriber sees state instantly without waiting for the next tick
- **Last Will and Testament** — if the publisher drops, the broker automatically announces it on `harbor/status/publisher`
- **Topic structure** — `harbor/crane/{id}/status` as a hierarchical namespace

## Subscribe from Bowire

1. Open [https://localhost:5117/bowire](https://localhost:5117/bowire)
2. Add broker URL `mqtt://localhost:1883` in the MQTT plugin settings
3. Subscribe to `harbor/crane/+/status`
4. Expect one frame per crane every second in the streaming view

## Minimum viable setup

```csharp
// 1. broker
var broker = new MqttServerFactory().CreateMqttServer(
    new MqttServerOptionsBuilder().WithDefaultEndpoint().Build());
await broker.StartAsync();

// 2. bowire
builder.Services.AddBowire();
app.MapBowire();
```
