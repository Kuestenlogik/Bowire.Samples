# Kuestenlogik.Bowire.Samples.Interceptor

Embedded Bowire with the **transparent in-process interceptor** switched
on. A single `app.UseBowireInterceptor()` tees every request flowing
through this host into the workbench's **Intercepted** rail — the one
embedding facet the other harbor-demo samples don't cover.

## What this sample demonstrates

- **`app.UseBowireInterceptor()`** (#153) — every request (any client,
  any tool) is captured into the Intercepted rail: method, path,
  headers, request + response body, status, latency. No client-side
  setup, no cert trust, no separate proxy process.
- **Self-exclusion** — the workbench's own `/bowire/*` surface is
  excluded by default, so the rail doesn't observe itself.
- **Recording auto-append** — with a recording open, intercepted flows
  land as recording steps automatically.
- **Discovery alongside interception** — the Rest plugin still reads
  `/openapi/v1.json`, so the seeded Harbor endpoints show up in Discover
  next to their captured traffic.

The domain is the shared **Harbor Control Center** store
(`Kuestenlogik.Bowire.Samples.Shared`), same as every other harbor-demo
sample — so the intercepted traffic tells the familiar Ships / Docks /
PortCalls story.

## Run it

```bash
dotnet run --project harbor-demo/src/Kuestenlogik.Bowire.Samples.Interceptor
```

Then browse the workbench at <https://localhost:5121/bowire>. Hit an
endpoint (e.g. `GET /api/ships`) from any client — curl, a browser, the
workbench's own invoke — and watch it appear in the **Intercepted** rail.
