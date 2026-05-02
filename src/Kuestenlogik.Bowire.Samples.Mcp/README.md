# Kuestenlogik.Bowire.Samples.Mcp

Isolated **Model Context Protocol** sample on `https://localhost:5119/bowire`.

Any MCP-aware agent (Claude Desktop, Cursor, Bowire's own MCP client) can call the harbor tools without knowing that gRPC / REST / etc. exist underneath.

## What this demonstrates

- **Tools** — AI-invocable methods with `[McpServerTool]`:
  - `schedule_port_call`
  - `find_free_dock`
  - `check_crane_status`
- **Resources** — static-ish data the agent reads by URI:
  - `harbor://ships`
  - `harbor://docks`
  - `harbor://port-calls`
- **HTTP/SSE transport** via `AddMcpServer().WithHttpTransport()`
- **Parameter descriptions** via `[Description]` — the agent sees these in the tool catalog

## Minimum viable setup

```csharp
builder.Services.AddBowire();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

app.MapMcp();
app.MapBowire();
```
