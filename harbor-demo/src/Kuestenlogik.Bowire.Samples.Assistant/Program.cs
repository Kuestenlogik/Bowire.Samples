// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Text.Json;
using Kuestenlogik.Bowire.Samples.Assistant;
using ModelContextProtocol.Server;

// Assistant — the AI ops assistant over the harbor, as an MCP server on the
// HTTP/SSE transport. An MCP-aware agent (Claude, Cursor, …) calls tools like
// `describe_port_call` and the Assistant fans out to the running services
// (PortCalls over GraphQL, Gate over REST) to answer — so the AI surface
// fronts the real landscape. A pure server; the Harbor.Gateway discovers it via
// the catalogue, or point a standalone workbench at mcp@http://localhost:5158.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<HarborGateway>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapMcp();
app.Run();

[McpServerToolType]
public sealed class HarborTools
{
    [McpServerTool(Name = "list_port_calls"),
     Description("List all port calls with their ship id and current status.")]
    public static async Task<string> ListPortCalls(HarborGateway harbor, CancellationToken ct)
    {
        var data = await harbor.GraphQLAsync("{ portCalls { id shipId status } }", ct);
        return Render(data, "portCalls");
    }

    [McpServerTool(Name = "describe_port_call"),
     Description("Describe one port call in full: its status, the ship (from Fleet), and the containers on that ship (from Gate). Demonstrates the cross-service fan-out.")]
    public static async Task<string> DescribePortCall(
        HarborGateway harbor,
        [Description("Port-call id")] int id,
        CancellationToken ct)
    {
        var query = $$"""
            { portCall(id: {{id}}) {
                id status
                ship { name flag type }
                dock { number maxDepthMeters hasCrane }
                containers { id owner status }
            } }
            """;
        var data = await harbor.GraphQLAsync(query, ct);
        return Render(data, "portCall");
    }

    [McpServerTool(Name = "containers_on_ship"),
     Description("List the containers currently loaded on a given ship (from the Gate service).")]
    public static async Task<string> ContainersOnShip(
        HarborGateway harbor,
        [Description("Ship id")] int shipId,
        CancellationToken ct)
    {
        var data = await harbor.ContainersOnShipAsync(shipId, ct);
        return data.ValueKind == JsonValueKind.Undefined
            ? "Gate service unavailable — start Kuestenlogik.Bowire.Samples.Gate."
            : data.GetRawText();
    }

    [McpServerTool(Name = "advance_port_call"),
     Description("Advance a port call one step through its lifecycle (Scheduled → Approaching → Docked → Departing → Completed).")]
    public static async Task<string> AdvancePortCall(
        HarborGateway harbor,
        [Description("Port-call id")] int id,
        CancellationToken ct)
    {
        var data = await harbor.GraphQLAsync(
            $"mutation {{ advancePortCall(id: {id}) {{ id status }} }}", ct);
        return Render(data, "advancePortCall");
    }

    private static string Render(JsonElement data, string field)
        => data.ValueKind == JsonValueKind.Undefined
            ? "PortCalls service unavailable — start Kuestenlogik.Bowire.Samples.PortCalls."
            : data.TryGetProperty(field, out var node)
                ? node.GetRawText()
                : data.GetRawText();
}
