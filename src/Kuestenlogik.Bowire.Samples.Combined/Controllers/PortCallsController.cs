// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Kuestenlogik.Bowire.Samples.Combined.Controllers;

/// <summary>
/// REST surface for port calls — mirrors the gRPC Unary SchedulePortCall
/// so a reader can compare the two idioms side-by-side in the Bowire
/// sidebar. Every endpoint is a plain HTTP verb against a resource.
/// </summary>
[ApiController]
[Route("api/port-calls")]
[Tags("Port Calls")]
public sealed class PortCallsController(HarborStore store) : ControllerBase
{
    [HttpGet]
    public IEnumerable<PortCall> List([FromQuery] PortCallStatus? status)
        => status is { } s
            ? store.PortCalls.Values.Where(pc => pc.Status == s)
            : store.PortCalls.Values;

    [HttpGet("{id:int}")]
    public ActionResult<PortCall> Get(int id)
        => store.PortCalls.TryGetValue(id, out var pc) ? Ok(pc) : NotFound();

    public sealed record SchedulePortCallBody(
        int ShipId,
        int DockNumber,
        DateTimeOffset ScheduledArrival,
        CargoOperation CargoOperation = CargoOperation.None,
        string? Notes = null);

    [HttpPost]
    public ActionResult<PortCall> Schedule([FromBody] SchedulePortCallBody body)
    {
        if (!store.Ships.ContainsKey(body.ShipId)) return NotFound($"Ship {body.ShipId} unknown");
        if (!store.Docks.ContainsKey(body.DockNumber)) return NotFound($"Dock {body.DockNumber} unknown");

        var id = store.NextPortCallId();
        var pc = new PortCall(
            Id: id,
            ShipId: body.ShipId,
            DockNumber: body.DockNumber,
            ScheduledArrival: body.ScheduledArrival,
            ActualArrival: null,
            ScheduledDeparture: null,
            ActualDeparture: null,
            Status: PortCallStatus.Scheduled,
            CargoOperation: body.CargoOperation,
            Notes: body.Notes);

        store.PortCalls[id] = pc;
        store.RaisePortCallChanged(pc);
        return CreatedAtAction(nameof(Get), new { id }, pc);
    }

    public sealed record StatusPatch(PortCallStatus Status, string? Notes);

    [HttpPatch("{id:int}/status")]
    public ActionResult<PortCall> SetStatus(int id, [FromBody] StatusPatch patch)
    {
        if (!store.PortCalls.TryGetValue(id, out var pc)) return NotFound();

        var updated = pc with
        {
            Status = patch.Status,
            Notes = patch.Notes ?? pc.Notes,
            ActualArrival  = patch.Status == PortCallStatus.Docked    && pc.ActualArrival  is null ? DateTimeOffset.UtcNow : pc.ActualArrival,
            ActualDeparture= patch.Status == PortCallStatus.Completed && pc.ActualDeparture is null ? DateTimeOffset.UtcNow : pc.ActualDeparture
        };
        store.PortCalls[id] = updated;
        store.RaisePortCallChanged(updated);
        return Ok(updated);
    }
}

[ApiController]
[Route("api/ships")]
[Tags("Ships")]
public sealed class ShipsController(HarborStore store) : ControllerBase
{
    [HttpGet] public IEnumerable<Ship> List() => store.Ships.Values;
    [HttpGet("{id:int}")]
    public ActionResult<Ship> Get(int id)
        => store.Ships.TryGetValue(id, out var s) ? Ok(s) : NotFound();
}

[ApiController]
[Route("api/docks")]
[Tags("Docks")]
public sealed class DocksController(HarborStore store) : ControllerBase
{
    [HttpGet] public IEnumerable<Dock> List() => store.Docks.Values;
    [HttpGet("{number:int}")]
    public ActionResult<Dock> Get(int number)
        => store.Docks.TryGetValue(number, out var d) ? Ok(d) : NotFound();
}
