# Kuestenlogik.Bowire.Samples.Gateway

The one **Bowire workbench over the whole harbor landscape**. It owns no
domain — it mounts Bowire and points the catalogue provider at a local
`harbor-catalogue.json` that lists every harbor microservice and the protocol
it speaks. Port `:5159`.

See [`harbor-demo/REDESIGN.md`](../../REDESIGN.md) for the full picture.

## What this sample demonstrates

- **Catalogue-driven discovery** — `AddBowire()` + `AddBowireCatalogue()` with
  the shipped `LocalCatalogueProvider` (`Bowire:Discovery:Catalogue:Provider =
  local`) reading `harbor-catalogue.json`. The Sources rail lists every entry;
  the browser connects **directly to each service's origin** (no reverse
  proxy). Swap `Provider` to `http` / `consul` / `kubernetes` to grow into a
  real deployment.
- **One workbench, many wires** — Fleet over gRPC and Inventory over OData
  show up side by side, discovered by protocol.

## Run it

Start the services, then the gateway:

```bash
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Fleet       # grpc  :5150
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Inventory   # odata :5151
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Gateway     # https://localhost:5159/bowire
```

Open <https://localhost:5159/bowire> — the harbor services appear in the
Sources rail, each ready to invoke over its own protocol.
