// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Kuestenlogik.Bowire.Samples.Rest.Controllers;

/// <summary>
/// Full CRUD surface for port calls — GET list / GET by id / POST /
/// PATCH status / DELETE. Demonstrates every common HTTP verb against
/// one resource. Query-parameter filter + 201-Created Location header
/// on POST are the details worth looking at in the Bowire response
/// pane.
/// </summary>
[ApiController]
[Route("api/port-calls")]
[Produces("application/json")]
public sealed class PortCallsController(HarborStore store) : ControllerBase
{
    /// GET /api/port-calls?status=Docked
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PortCall>), StatusCodes.Status200OK)]
    public IEnumerable<PortCall> List([FromQuery] PortCallStatus? status)
        => status is { } s
            ? store.PortCalls.Values.Where(pc => pc.Status == s)
            : store.PortCalls.Values;

    /// GET /api/port-calls/{id}
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PortCall), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PortCall> Get(int id)
        => store.PortCalls.TryGetValue(id, out var pc) ? Ok(pc) : NotFound();

    public sealed record SchedulePortCallBody(
        int ShipId,
        int DockNumber,
        DateTimeOffset ScheduledArrival,
        CargoOperation CargoOperation = CargoOperation.None,
        string? Notes = null);

    /// POST /api/port-calls
    [HttpPost]
    [ProducesResponseType(typeof(PortCall), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<PortCall> Schedule([FromBody] SchedulePortCallBody body)
    {
        if (!store.Ships.ContainsKey(body.ShipId))
            return Problem(title: "Unknown ship", detail: $"Ship {body.ShipId} unknown", statusCode: 404);
        if (!store.Docks.ContainsKey(body.DockNumber))
            return Problem(title: "Unknown dock", detail: $"Dock {body.DockNumber} unknown", statusCode: 404);
        if (body.ScheduledArrival < DateTimeOffset.UtcNow.AddMinutes(-1))
            return Problem(title: "Invalid arrival", detail: "Arrival time is in the past", statusCode: 400);

        var id = store.NextPortCallId();
        var pc = new PortCall(
            Id: id, ShipId: body.ShipId, DockNumber: body.DockNumber,
            ScheduledArrival: body.ScheduledArrival,
            ActualArrival: null, ScheduledDeparture: null, ActualDeparture: null,
            Status: PortCallStatus.Scheduled,
            CargoOperation: body.CargoOperation,
            Notes: body.Notes);

        store.PortCalls[id] = pc;
        store.RaisePortCallChanged(pc);
        return CreatedAtAction(nameof(Get), new { id }, pc);
    }

    public sealed record StatusPatch(PortCallStatus Status, string? Notes);

    /// PATCH /api/port-calls/{id}/status
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

    /// DELETE /api/port-calls/{id}
    [HttpDelete("{id:int}")]
    public IActionResult Cancel(int id)
    {
        if (!store.PortCalls.TryRemove(id, out _)) return NotFound();
        return NoContent();
    }
}

/// <summary>
/// Multipart upload — drop a plain-text manifest file, one container
/// entry per line: <c>ContainerId,WeightKg,Owner,ForShipId</c>. The
/// endpoint appears in the Bowire request pane with a file-picker
/// in place of the usual JSON editor.
/// </summary>
[ApiController]
[Route("api/manifests")]
public sealed class ManifestsController(HarborStore store, ILogger<ManifestsController> log) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file.Length == 0) return BadRequest("Empty upload");

        int lines = 0, accepted = 0, skipped = 0;
        using var reader = new StreamReader(file.OpenReadStream());
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 4) { skipped++; continue; }

            if (!decimal.TryParse(parts[1], out var kg)) { skipped++; continue; }
            if (!int.TryParse(parts[3], out var shipId)) { skipped++; continue; }
            if (store.Containers.ContainsKey(parts[0])) { skipped++; continue; }

            store.Containers[parts[0]] = new Container(
                Id: parts[0], WeightKg: kg, Owner: parts[2],
                Status: ContainerStatus.Stored, OnShipId: shipId == 0 ? null : shipId);
            accepted++;
        }
        log.LogInformation("Manifest upload: {Accepted} accepted, {Skipped} skipped", accepted, skipped);
        return Ok(new { lines, accepted, skipped });
    }
}

[ApiController]
[Route("api/ships")]
public sealed class ShipsController(HarborStore store) : ControllerBase
{
    [HttpGet] public IEnumerable<Ship> List() => store.Ships.Values;
    [HttpGet("{id:int}")]
    public ActionResult<Ship> Get(int id)
        => store.Ships.TryGetValue(id, out var s) ? Ok(s) : NotFound();
}

[ApiController]
[Route("api/docks")]
public sealed class DocksController(HarborStore store) : ControllerBase
{
    [HttpGet] public IEnumerable<Dock> List([FromQuery] bool? free)
    {
        var all = store.Docks.Values;
        return free is true ? all.Where(d => d.OccupiedByShipId is null) : all;
    }
}
