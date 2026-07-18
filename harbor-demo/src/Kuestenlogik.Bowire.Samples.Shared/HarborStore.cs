// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace Kuestenlogik.Bowire.Samples.Shared;

/// <summary>
/// In-memory harbor state shared by every sample project. Not thread-safe
/// beyond what ConcurrentDictionary gives us — demos are single-user, a
/// real system would back this with a DB and a proper unit-of-work.
///
/// Seed data is intentionally small (3 ships, 5 docks, 3 cranes, ~15
/// containers, 2 active port calls) so screenshots and tutorials show a
/// busy-but-readable harbor.
/// </summary>
public sealed class HarborStore
{
    public ConcurrentDictionary<int, Ship> Ships { get; } = new();
    public ConcurrentDictionary<int, Dock> Docks { get; } = new();
    public ConcurrentDictionary<int, Crane> Cranes { get; } = new();
    public ConcurrentDictionary<string, Container> Containers { get; } = new();
    public ConcurrentDictionary<int, PortCall> PortCalls { get; } = new();

    int _portCallSeq;
    public int NextPortCallId() => Interlocked.Increment(ref _portCallSeq);

    /// <summary>Published whenever a PortCall's status changes — SignalR / SSE
    /// / WebSocket handlers subscribe here to broadcast updates.</summary>
    public event Action<PortCall>? PortCallChanged;
    public void RaisePortCallChanged(PortCall pc) => PortCallChanged?.Invoke(pc);

    public static HarborStore CreateSeeded()
    {
        // Seed data now lives in the pure, stateless HarborSeed factory so
        // the microservices (each with its own private store) share it too.
        // This store is the legacy monolith view — the same values, byte for
        // byte, just materialised into the mutable dictionaries + event hub.
        var s = new HarborStore();

        foreach (var d in HarborSeed.Docks()) s.Docks[d.Number] = d;
        foreach (var c in HarborSeed.Cranes()) s.Cranes[c.Id] = c;
        foreach (var ship in HarborSeed.Ships()) s.Ships[ship.Id] = ship;
        foreach (var c in HarborSeed.Containers()) s.Containers[c.Id] = c;
        foreach (var pc in HarborSeed.PortCalls(DateTimeOffset.UtcNow)) s.PortCalls[pc.Id] = pc;

        // Continue the id sequence past the seeded port calls so a freshly
        // scheduled one gets the next free id (4, 5, …), never a collision.
        s._portCallSeq = HarborSeed.MaxPortCallId;

        return s;
    }
}
