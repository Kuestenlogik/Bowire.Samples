// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Samples.Shared;

/// <summary>
/// The single source of the harbor demo's seed data — a pure, stateless
/// factory with no store, no mutation, no events. Every service (and the
/// legacy <see cref="HarborStore"/>) seeds its own private state from here,
/// so the same 3 ships / 5 docks / 3 cranes / 6 containers / 3 port calls
/// appear everywhere and screenshots stay identical across the landscape.
///
/// Ids are fixed (not sequence-generated) so independent services agree on
/// them by value — the microservices redesign references entities across
/// contexts by id only (see harbor-demo/REDESIGN.md). Time-relative data
/// (port-call arrival/departure) takes an explicit <paramref name="now"/> so
/// callers can freeze the clock for deterministic demos.
/// </summary>
public static class HarborSeed
{
    public static IReadOnlyList<Ship> Ships() =>
    [
        new(Id: 101, Name: "Nordstern", Flag: "DE", LengthMeters: 210, Type: ShipType.Container),
        new(Id: 102, Name: "Isabella",  Flag: "NL", LengthMeters: 180, Type: ShipType.Bulk),
        new(Id: 103, Name: "Aurora",    Flag: "NO", LengthMeters: 240, Type: ShipType.Tanker),
    ];

    // Five docks, varying depth. Docks 1 + 2 have cranes (container ops),
    // Dock 3 is for bulk (no crane, ship-own gear), Docks 4 + 5 are free so
    // the demo shows "available" slots.
    public static IReadOnlyList<Dock> Docks() =>
    [
        new(1, 14.5m, HasCrane: true,  OccupiedByShipId: 101),
        new(2, 12.0m, HasCrane: true,  OccupiedByShipId: null),
        new(3, 16.0m, HasCrane: false, OccupiedByShipId: 102),
        new(4, 10.0m, HasCrane: false, OccupiedByShipId: null),
        new(5, 11.5m, HasCrane: false, OccupiedByShipId: null),
    ];

    // Crane 1 is lifting MSCU1234567, which is not an arbitrary pairing: crane 1
    // works Dock 1, Dock 1 is occupied by ship 101, and MSCU1234567 is the
    // container being loaded onto ship 101 (below). The relationship was always
    // implied by this data; naming it is what lets the telemetry stream join the
    // REST and GraphQL steps on a value that means something.
    public static IReadOnlyList<Crane> Cranes() =>
    [
        new(Id: 1, DockNumber: 1, MaxLiftTonnes: 50m, Status: CraneStatus.Lifting,
            LiftingContainerId: "MSCU1234567"),
        new(Id: 2, DockNumber: 2, MaxLiftTonnes: 50m, Status: CraneStatus.Idle),
        new(Id: 3, DockNumber: 2, MaxLiftTonnes: 80m, Status: CraneStatus.Maintenance),
    ];

    // Some containers in the yard, some on ship 101 (at Dock 1 being loaded).
    public static IReadOnlyList<Container> Containers() =>
    [
        new("MSCU1234567", 18_500m, "MSC",       ContainerStatus.Loading, OnShipId: 101),
        new("HLBU2345678", 22_000m, "Hapag",     ContainerStatus.Loading, OnShipId: 101),
        new("EGHU3456789", 14_800m, "Evergreen", ContainerStatus.OnShip,  OnShipId: 101),
        new("MSKU4567890", 20_100m, "Maersk",    ContainerStatus.Stored,  OnShipId: null),
        new("CMAU5678901", 16_400m, "CMA CGM",   ContainerStatus.Stored,  OnShipId: null),
        new("OOLU6789012", 19_900m, "OOCL",      ContainerStatus.Stored,  OnShipId: null),
    ];

    /// <summary>
    /// Three port calls (ids 1-3) relative to <paramref name="now"/>: two
    /// docked + one scheduled, seeding a little live-plus-history.
    /// </summary>
    public static IReadOnlyList<PortCall> PortCalls(DateTimeOffset now) =>
    [
        new(Id: 1, ShipId: 101, DockNumber: 1,
            ScheduledArrival: now.AddHours(-6), ActualArrival: now.AddHours(-5).AddMinutes(50),
            ScheduledDeparture: now.AddHours(4), ActualDeparture: null,
            Status: PortCallStatus.Docked, CargoOperation: CargoOperation.Loading, Notes: "On schedule"),
        new(Id: 2, ShipId: 102, DockNumber: 3,
            ScheduledArrival: now.AddHours(-2), ActualArrival: now.AddMinutes(-90),
            ScheduledDeparture: now.AddHours(6), ActualDeparture: null,
            Status: PortCallStatus.Docked, CargoOperation: CargoOperation.Unloading, Notes: null),
        new(Id: 3, ShipId: 103, DockNumber: 4,
            ScheduledArrival: now.AddHours(3), ActualArrival: null,
            ScheduledDeparture: now.AddHours(20), ActualDeparture: null,
            Status: PortCallStatus.Scheduled, CargoOperation: CargoOperation.Both, Notes: "ETA confirmed via ship's radio"),
    ];

    /// <summary>Highest seeded PortCall id — freshly created port calls start after this.</summary>
    public static int MaxPortCallId => 3;
}
