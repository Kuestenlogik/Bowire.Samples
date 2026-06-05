# Bowire reference dashboards

Grafana dashboards consuming the metrics + traces Bowire emits via its self-telemetry seam (issue [Kuestenlogik/Bowire#29](https://github.com/Kuestenlogik/Bowire/issues/29)). Import into Grafana 11+ pointed at the Prometheus / Mimir datasource that scrapes your OpenTelemetry collector.

## `bowire-overview.json`

Headline dashboard covering the four panel groups Bowire emits today:

| Group | Panels |
|---|---|
| **Invoke** | invokes / s, error ratio, p50 / p95 / p99 duration, breakdown by protocol |
| **Discover** | discovery probes by outcome and by protocol |
| **Mock** | UI-started mock requests by outcome (matched / miss / 404) and by HTTP status |
| **Plugin lifecycle** | startup load attempts (loaded / disabled) per plugin id |

Template variables:

- `$datasource` — the Prometheus datasource. Defaults to the one named `prometheus`.
- `$protocol` — filters the Invoke panels by Bowire's protocol id (`rest`, `grpc`, `mqtt`, `nats`, …). `All` by default.

## Wiring Bowire's signals into your collector

Bowire emits OTLP via the canonical `Kuestenlogik.Bowire` Meter and ActivitySource. The standalone CLI wires the exporter on `--telemetry`; the wire endpoint / headers / protocol come from the standard `OTEL_EXPORTER_OTLP_*` env vars. Example pointing at a local collector:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
OTEL_RESOURCE_ATTRIBUTES=service.name=bowire,service.namespace=dev \
bowire --url http://localhost:5001 --telemetry
```

For shared multi-tenant installs, add `--telemetry-strip-method-labels` to drop the per-service / per-method cardinality before export.

## Embedded hosts

Embedded hosts skip `AddBowireTelemetry` and add the Source / Meter to their existing pipeline:

```csharp
builder.Services.AddOpenTelemetry()
  .WithMetrics(m => m.AddMeter(BowireTelemetry.MeterName))
  .WithTracing(t  => t.AddSource(BowireTelemetry.ActivityName));
```

The dashboard reads `bowire_*_total` / `bowire_*_milliseconds_*` series — Prometheus's default OTLP naming. No relabeling required.
