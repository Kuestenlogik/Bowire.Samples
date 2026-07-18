# Kuestenlogik.Bowire.Samples.Fleet

The **vessel-registry** microservice of the harbor landscape (see
[`harbor-demo/REDESIGN.md`](../../REDESIGN.md)). A pure **gRPC** server —
contract-first `.proto`, HTTP/2 on `:5150`, no embedded Bowire.

## What this service owns

`Ship` master data (`ShipId` is the authority every other context references
by id). Seeded from `HarborSeed.Ships()` into this service's own private
store — nothing shared.

## What this sample demonstrates

- **Contract-first gRPC** for stable, strongly-typed reference data:
  `GetShip` (unary) + `ListShips` (server-stream).
- **gRPC reflection**, so Bowire discovers the service over a plain
  `grpc@http://…` URL with no bundled descriptor.

## Run it

```bash
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Fleet
```

Discover it from the **Harbor.Gateway** (`/bowire`), or standalone:

```bash
bowire --url grpc@http://localhost:5150
```
