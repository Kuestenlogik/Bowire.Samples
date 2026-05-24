# Kuestenlogik.Bowire.Samples.Amqp

Isolated **AMQP 0.9.1** sample. The process runs two things:

1. a **publisher** that pushes crane telemetry every second to a RabbitMQ topic exchange
2. the **Bowire UI** on `https://localhost:5118/bowire`

Unlike the MQTT sample, this one needs an **external broker** — `.NET` has no in-process AMQP 0.9.1 server, so the project ships a `compose.yaml` for RabbitMQ next to it.

## Quick start

```bash
# 1. Start the broker (15672 = Management API, 5672 = AMQP)
docker compose up -d rabbitmq

# 2. Run the sample
dotnet run
```

That's it. The broker takes a few seconds to come up; the publisher retries until it can connect.

## What this demonstrates

- **Topic exchange routing** — `harbor` is a topic exchange; messages are published with routing keys like `crane.{id}.status`. Subscribers can bind queues with wildcards: `crane.*.status` for one crane per slot, `crane.#` for the whole branch.
- **Persistent delivery** — `Persistent = true` on `BasicProperties` so the broker keeps queued messages across restarts. The AMQP 0.9.1 equivalent of MQTT's retain flag is a `x-message-ttl` + bound queue, but persistent delivery is close enough for a demo.
- **Topology discovery via the Management API** — Bowire's AMQP plugin uses `:15672/api/exchanges`, `/queues`, `/bindings` to populate the sidebar before subscribing.
- **Routing-key namespacing** — `crane.{id}.status` is the AMQP equivalent of MQTT's `harbor/crane/{id}/status` topic; Bowire renders both as a hierarchy in the sidebar.

## Subscribe from Bowire

### Embedded workbench

Open [https://localhost:5118/bowire](https://localhost:5118/bowire). The AMQP plugin connects to `amqp://localhost:5672` and shows the `harbor` exchange in the sidebar. Click *Consume* on `crane.*.status` to start the stream — one frame per crane appears in the streaming pane every second.

### Standalone CLI

```bash
bowire --url amqp://localhost:5672
```

Same picker, same streaming pane.

## Bind a queue from the AMQP side

If you want to see what Bowire is consuming, the RabbitMQ Management UI at [http://localhost:15672](http://localhost:15672) (user / pass: `guest` / `guest`) lets you bind a debug queue:

1. Queues → Add a new queue → name `debug-cranes`, type `classic`
2. Exchanges → `harbor` → Bindings → To `debug-cranes`, routing key `crane.#`
3. Queues → `debug-cranes` → Get messages

Bowire receives the same messages because the exchange fans out to every bound queue.

## AMQP 1.0 variant

For AMQP 1.0 (Solace, Azure Service Bus, Artemis, …) connect Bowire at `amqp1://broker:5672` instead. The Bowire AMQP plugin picks the wire from the URL scheme; the publisher pattern stays the same, only the client library swaps (`AMQPNetLite` instead of `RabbitMQ.Client`). See the [TacticalAPI sample](../Kuestenlogik.Bowire.Samples.TacticalApi/README.md) for an analogous-shaped sample, plus the [plugin repo's README](https://github.com/Kuestenlogik/Bowire.Protocol.Amqp) for the URL-scheme reference.

## Teardown

```bash
docker compose down
```
