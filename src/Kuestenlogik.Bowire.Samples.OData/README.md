# Kuestenlogik.Bowire.Samples.OData

Isolated **OData v4** sample on `https://localhost:5116/bowire`. Metadata at `/odata/$metadata`.

## What this demonstrates

- **`$filter`** — `/odata/PortCalls?$filter=Status eq 'Docked'`
- **`$expand`** — `/odata/PortCalls?$expand=Ship,Dock`
- **`$select`** — `/odata/Ships?$select=Id,Name,Flag`
- **`$orderby`** — `/odata/PortCalls?$orderby=ScheduledArrival desc`
- **`$top` / `$skip`** — `/odata/Ships?$top=2&$skip=1`
- **`$count`** — `/odata/PortCalls?$count=true`
- **Deep expansion** — `MaxExpansionDepth = 4` on Ships + PortCalls
- **EDM schema** — Microsoft.OData.ModelBuilder auto-discovers keys
  (with explicit overrides for `Dock.Number` and `Container.Id`)

## Entity sets

`Ships` / `Docks` / `Cranes` / `PortCalls` / `Containers`

## Minimum viable setup

```csharp
var edm = new ODataConventionModelBuilder();
edm.EntitySet<MyEntity>("MyEntities");

builder.Services.AddControllers().AddOData(opt => opt
    .Select().Filter().OrderBy().Expand().Count()
    .AddRouteComponents("odata", edm.GetEdmModel())
);
builder.Services.AddBowire();

app.MapControllers();
app.MapBowire();
```
