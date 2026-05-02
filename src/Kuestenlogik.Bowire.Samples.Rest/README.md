# Kuestenlogik.Bowire.Samples.Rest

Isolated **REST-only** sample. Harbor domain exposed through every
common HTTP verb, with the details that usually bite first when you
add a real REST service to Bowire.

## What this sample demonstrates

- **Every HTTP verb** on one resource (`/api/port-calls`):
  GET (list + by id), POST, PATCH, DELETE
- **Query-parameter filter**: `GET /api/port-calls?status=Docked`
- **OpenAPI schema** at `/openapi/v1.json` — consumed by the REST plugin
  for discovery. Bowire falls back to `ApiExplorer` in embedded mode.
- **ProblemDetails** for 4xx responses, not plain strings
- **201-Created** with a proper `Location` header on POST
- **Multipart upload** — `POST /api/manifests/upload` takes a text file
  (one container entry per line). The Bowire request pane renders a
  file picker automatically.

## Run it

```bash
dotnet run --project samples/Kuestenlogik.Bowire.Samples.Rest
```

Open [https://localhost:5111/bowire](https://localhost:5111/bowire).
Expected sidebar: three controllers (PortCalls, Manifests, Ships, Docks)
with a handful of methods each.

## Minimum viable setup

```csharp
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddBowire();      // <-- picks up the REST plugin

app.MapOpenApi();
app.MapControllers();
app.MapBowire();
```
