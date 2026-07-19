// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.PortCalls;

/// <summary>
/// PortCalls' own private store — the port-call orchestration aggregate it
/// owns, seeded from <see cref="HarborSeed"/>. It holds only the port-call
/// record itself; the ship, dock and containers a port call touches live in
/// other services and are fetched across the wire on demand (see the BFF
/// resolvers). <see cref="Advance"/> drives the port-call state machine.
/// </summary>
public sealed class PortCallStore
{
    private readonly Dictionary<int, PortCall> _calls =
        HarborSeed.PortCalls(DateTimeOffset.UtcNow).ToDictionary(p => p.Id);

    public IEnumerable<PortCall> All => _calls.Values;

    public PortCall? Find(int id) => _calls.TryGetValue(id, out var pc) ? pc : null;

    /// <summary>
    /// Advance one step through the port-call lifecycle
    /// (Scheduled → Approaching → Docked → Departing → Completed). Completed
    /// and Cancelled are terminal. Returns null if the id is unknown.
    /// </summary>
    public PortCall? Advance(int id)
    {
        if (!_calls.TryGetValue(id, out var pc)) return null;
        var next = pc.Status switch
        {
            PortCallStatus.Scheduled => PortCallStatus.Approaching,
            PortCallStatus.Approaching => PortCallStatus.Docked,
            PortCallStatus.Docked => PortCallStatus.Departing,
            PortCallStatus.Departing => PortCallStatus.Completed,
            _ => pc.Status,
        };
        var updated = pc with { Status = next };
        _calls[id] = updated;
        return updated;
    }
}
