// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using Kuestenlogik.Bowire.Samples.Shared;
using ModelContextProtocol.Server;

// Isolated MCP (Model Context Protocol) sample. Exposes the harbor as
// a set of AI-callable tools plus a couple of resources, over the
// HTTP/SSE transport. AI clients like Claude, Cursor, or any MCP-aware
// agent can invoke `schedule_port_call` and read `harbor://ships`
// directly without knowing anything about gRPC / REST / etc.
//
// Discovery is external — Bowire's MCP plugin is a client that browses
// a remote MCP server. Browse this sample with a standalone Bowire:
//   bowire --url https://localhost:5119/

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(HarborStore.CreateSeeded());

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

var app = builder.Build();

app.MapMcp();
app.Run();

// --------------------------------------------------------
// Tools — AI-callable functions
// --------------------------------------------------------
[McpServerToolType]
public sealed class HarborTools
{
    [McpServerTool(Name = "schedule_port_call"),
     Description("Schedule a new port call for a ship at a specific dock.")]
    public static PortCall SchedulePortCall(
        HarborStore store,
        [Description("Ship ID to schedule the call for")] int shipId,
        [Description("Dock number to reserve")] int dockNumber,
        [Description("Scheduled arrival (ISO-8601 UTC)")] DateTimeOffset scheduledArrival,
        [Description("Cargo operation: Loading, Unloading, Both, or None")] CargoOperation cargoOperation = CargoOperation.None)
    {
        var id = store.NextPortCallId();
        var pc = new PortCall(
            Id: id, ShipId: shipId, DockNumber: dockNumber,
            ScheduledArrival: scheduledArrival,
            ActualArrival: null, ScheduledDeparture: null, ActualDeparture: null,
            Status: PortCallStatus.Scheduled,
            CargoOperation: cargoOperation, Notes: null);
        store.PortCalls[id] = pc;
        store.RaisePortCallChanged(pc);
        return pc;
    }

    [McpServerTool(Name = "find_free_dock"),
     Description("Find the first free dock with enough depth and crane availability for a ship.")]
    public static object? FindFreeDock(
        HarborStore store,
        [Description("Minimum required dock depth in metres")] decimal minDepthMeters = 10,
        [Description("Does the dock need a crane?")] bool requireCrane = false)
    {
        foreach (var d in store.Docks.Values)
        {
            if (d.OccupiedByShipId is not null) continue;
            if (d.MaxDepthMeters < minDepthMeters) continue;
            if (requireCrane && !d.HasCrane) continue;
            return d;
        }
        return null;
    }

    [McpServerTool(Name = "check_crane_status"),
     Description("Return the current status and last load of a crane.")]
    public static Crane? CheckCraneStatus(HarborStore store,
        [Description("Crane ID")] int craneId)
        => store.Cranes.TryGetValue(craneId, out var c) ? c : null;
}

// --------------------------------------------------------
// Resources — static(-ish) data the agent can read by URI
// --------------------------------------------------------
[McpServerResourceType]
public sealed class HarborResources
{
    [McpServerResource(UriTemplate = "harbor://ships", Name = "All ships"),
     Description("The complete fleet registered in the harbor.")]
    public static IEnumerable<Ship> AllShips(HarborStore store) => store.Ships.Values;

    [McpServerResource(UriTemplate = "harbor://docks", Name = "All docks"),
     Description("Every dock with current occupancy.")]
    public static IEnumerable<Dock> AllDocks(HarborStore store) => store.Docks.Values;

    [McpServerResource(UriTemplate = "harbor://port-calls", Name = "Active port calls"),
     Description("Currently-active port calls (not yet completed or cancelled).")]
    public static IEnumerable<PortCall> ActivePortCalls(HarborStore store)
        => store.PortCalls.Values.Where(pc =>
            pc.Status != PortCallStatus.Completed && pc.Status != PortCallStatus.Cancelled);
}
