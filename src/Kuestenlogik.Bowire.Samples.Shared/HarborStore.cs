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
        var s = new HarborStore();

        // Five docks, varying depth. Docks 1 + 2 have cranes (container ops),
        // Dock 3 is for bulk (no crane, ship-own gear), Docks 4 + 5 are free
        // for the demo to show "available" slots.
        foreach (var d in new[]
        {
            new Dock(1, 14.5m, HasCrane: true,  OccupiedByShipId: 101),
            new Dock(2, 12.0m, HasCrane: true,  OccupiedByShipId: null),
            new Dock(3, 16.0m, HasCrane: false, OccupiedByShipId: 102),
            new Dock(4, 10.0m, HasCrane: false, OccupiedByShipId: null),
            new Dock(5, 11.5m, HasCrane: false, OccupiedByShipId: null),
        })
        {
            s.Docks[d.Number] = d;
        }

        foreach (var c in new[]
        {
            new Crane(Id: 1, DockNumber: 1, MaxLiftTonnes: 50m, Status: CraneStatus.Lifting),
            new Crane(Id: 2, DockNumber: 2, MaxLiftTonnes: 50m, Status: CraneStatus.Idle),
            new Crane(Id: 3, DockNumber: 2, MaxLiftTonnes: 80m, Status: CraneStatus.Maintenance),
        })
        {
            s.Cranes[c.Id] = c;
        }

        foreach (var ship in new[]
        {
            new Ship(Id: 101, Name: "Nordstern",   Flag: "DE", LengthMeters: 210, Type: ShipType.Container),
            new Ship(Id: 102, Name: "Isabella",    Flag: "NL", LengthMeters: 180, Type: ShipType.Bulk),
            new Ship(Id: 103, Name: "Aurora",      Flag: "NO", LengthMeters: 240, Type: ShipType.Tanker),
        })
        {
            s.Ships[ship.Id] = ship;
        }

        // A handful of containers — some in the yard, some on ship 101 which
        // is at Dock 1 being loaded.
        var containers = new List<Container>
        {
            new("MSCU1234567", 18_500m, "MSC",       ContainerStatus.Loading, OnShipId: 101),
            new("HLBU2345678", 22_000m, "Hapag",     ContainerStatus.Loading, OnShipId: 101),
            new("EGHU3456789", 14_800m, "Evergreen", ContainerStatus.OnShip,  OnShipId: 101),
            new("MSKU4567890", 20_100m, "Maersk",    ContainerStatus.Stored,  OnShipId: null),
            new("CMAU5678901", 16_400m, "CMA CGM",   ContainerStatus.Stored,  OnShipId: null),
            new("OOLU6789012", 19_900m, "OOCL",      ContainerStatus.Stored,  OnShipId: null),
        };
        foreach (var c in containers) s.Containers[c.Id] = c;

        // Two active port calls + one completed to seed the history.
        var now = DateTimeOffset.UtcNow;
        foreach (var pc in new[]
        {
            new PortCall(
                Id: s.NextPortCallId(), ShipId: 101, DockNumber: 1,
                ScheduledArrival: now.AddHours(-6), ActualArrival: now.AddHours(-5).AddMinutes(50),
                ScheduledDeparture: now.AddHours(4), ActualDeparture: null,
                Status: PortCallStatus.Docked, CargoOperation: CargoOperation.Loading,
                Notes: "On schedule"),
            new PortCall(
                Id: s.NextPortCallId(), ShipId: 102, DockNumber: 3,
                ScheduledArrival: now.AddHours(-2), ActualArrival: now.AddMinutes(-90),
                ScheduledDeparture: now.AddHours(6), ActualDeparture: null,
                Status: PortCallStatus.Docked, CargoOperation: CargoOperation.Unloading,
                Notes: null),
            new PortCall(
                Id: s.NextPortCallId(), ShipId: 103, DockNumber: 4,
                ScheduledArrival: now.AddHours(3), ActualArrival: null,
                ScheduledDeparture: now.AddHours(20), ActualDeparture: null,
                Status: PortCallStatus.Scheduled, CargoOperation: CargoOperation.Both,
                Notes: "ETA confirmed via ship's radio")
        })
        {
            s.PortCalls[pc.Id] = pc;
        }

        return s;
    }
}
