# Kuestenlogik.Bowire.Samples.Inventory

The **physical-assets** microservice of the harbor landscape (see
[`harbor-demo/REDESIGN.md`](../../REDESIGN.md)). A pure **OData v4** server on
`:5151`, no embedded Bowire.

## What this service owns

`Dock` and static `Crane` config, seeded from `HarborSeed` into its own
private store. Live crane *status* lives in the Telemetry service, keyed by the
same `CraneId` — the shared-kernel-by-id split.

## What this sample demonstrates

- **OData v4** over a bounded slice: `$filter`, `$orderby`, `$select`,
  `$expand`, `$count` on `Docks` + `Cranes` via `$metadata` discovery and
  Bowire's query-option builder.

```
GET /odata/Docks?$filter=HasCrane eq true&$orderby=MaxDepthMeters desc
GET /odata/Cranes?$filter=Status eq 'Maintenance'
```

## Run it

```bash
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Inventory
```

Discover it from the **Harbor.Gateway** (`/bowire`), or standalone:

```bash
bowire --url odata@https://localhost:5151/odata
```
