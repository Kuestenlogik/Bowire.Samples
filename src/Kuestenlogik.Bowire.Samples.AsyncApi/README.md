# Kuestenlogik.Bowire.Samples.AsyncApi

**AsyncAPI** sample — the schema-side companion to the [`Mqtt`](../Kuestenlogik.Bowire.Samples.Mqtt) sample.

AsyncAPI is the OpenAPI equivalent for event-driven APIs: a single YAML/JSON document that describes channels, operations, messages, and which transport binding (MQTT, Kafka, WebSocket, AMQP, …) each channel actually rides on. It carries *no wire of its own* — under the covers it always delegates to the real broker.

This sample therefore opens **no sockets**. All it does is publish `harbor-events.asyncapi.yaml` at `https://localhost:5120/asyncapi.yaml`. The actual MQTT broker comes from the sibling Mqtt sample on `mqtt://localhost:1883`.

## What this demonstrates

- **Schema-driven discovery** — Bowire reads the AsyncAPI document, walks `channels[]` + `operations[]`, and surfaces them as sidebar entries just like it would for a discovered gRPC service or REST endpoint.
- **`send` / `receive` direction** — operations tagged `receive` (broker → us) become server-streaming methods; `send` (us → broker) become client-streaming. AsyncAPI tags polarity from the application's perspective; Bowire is the test client and flips it once in the AsyncAPI plugin's mapping layer.
- **Cross-plugin routing** — invocations don't run in this sample's process. The AsyncAPI plugin looks up the matching wire plugin (here: the MQTT plugin) through `BowireProtocolRegistry` and forwards the call. No hard dependency between the schema source and the transport.
- **Same `BOWIRE_PROTOCOLS` topology you saw in the Mqtt sample** — `harbor/crane/{craneId}/status` and `harbor/status/publisher` are the topics the Mqtt sample's publisher emits. The document doesn't fabricate new topics; it just describes the ones that exist.

## Run it

```bash
# 1. In one terminal — start the Mqtt sample (broker on 1883)
dotnet run --project src/Kuestenlogik.Bowire.Samples.Mqtt

# 2. In another terminal — start the schema server (port 5120)
dotnet run --project src/Kuestenlogik.Bowire.Samples.AsyncApi

# 3. In a third — point Bowire at both. The AsyncAPI doc carries the
#    channel + operation tree; the broker URL is the actual wire.
bowire --url https://localhost:5120/asyncapi.yaml \
       --url mqtt://localhost:1883
```

## What you should see in the workbench

- Two `services` in the sidebar (one per channel) — `craneStatus` and `publisherStatus`, both tagged with the **AsyncAPI** plugin marker (cyan→purple→magenta gradient).
- One method per operation — `receiveCraneStatus`, `receivePublisherStatus`. Both render as server-streaming arrows because their `action` is `receive`.
- The channel address (`harbor/crane/{craneId}/status`) appears as a sub-label so the topic is visible without expanding the method.
- Invoking either method opens a subscription on the MQTT broker. Frames stream in identically to invoking from the MQTT plugin directly — same wire, just discovered through the schema.

## Why this matters

The Mqtt sample shows MQTT working. This sample shows that an AsyncAPI document — the format teams already write and publish for their event-driven APIs — drops straight into Bowire's discovery flow without anyone having to know which Bowire plugin they need. AsyncAPI authors keep their existing tooling; Bowire becomes another consumer that respects the schema.

## What's coming

- **Bindings extraction (Phase A4b)** — `bindings.mqtt.qos`, `.retain`, `.topic` from the doc will populate the invocation metadata, currently defaulted at `AtLeastOnce` / `retain=false`.
- **Kafka + WebSocket bindings (Phase B)** — duplicate this sample with a Kafka or WS-targeted document to demonstrate the same loader feeding into a different wire plugin.
- **AsyncAPI 2.x support (Phase A4)** — today the loader only maps 3.0 documents; the SDK reads 2.x fine but the channel walker for that schema is still to write.
