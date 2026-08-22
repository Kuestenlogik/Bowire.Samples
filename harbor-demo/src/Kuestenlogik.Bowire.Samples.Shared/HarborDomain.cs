// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Samples.Shared;

// Harbor domain for every Bowire.Samples.* project. The domain is
// deliberately flat and non-maritime-jargon so the samples stay about
// Bowire, not about shipping vocabulary. Entity dictionary:
//
//   Ship         — a vessel (container / bulk / tanker)
//   Dock         — a harbor berth with optional crane
//   Crane        — a crane attached to a dock
//   Container    — a container stored / loading / on a ship
//   PortCall     — a visit event: ship arrives, docks, leaves
//
// The PortCall status flow:
//
//   Scheduled → Approaching → Docked → Departing → Completed
//        │
//        └─► Cancelled
//
// All samples import this assembly to share the entity shapes. The
// in-memory HarborStore lives here too so every sample boots with the
// same three ships / five docks / twenty-odd containers. That way
// screenshots and tutorials stay consistent across projects.

public enum ShipType { Container, Bulk, Tanker }
public enum CraneStatus { Idle, Lifting, Moving, Maintenance }
public enum ContainerStatus { Stored, Loading, OnShip }
public enum CargoOperation { Loading, Unloading, Both, None }

public enum PortCallStatus
{
    Scheduled,
    Approaching,
    Docked,
    Departing,
    Completed,
    Cancelled
}

public sealed record Ship(
    int Id,
    string Name,
    string Flag,
    int LengthMeters,
    ShipType Type);

public sealed record Dock(
    int Number,
    decimal MaxDepthMeters,
    bool HasCrane,
    int? OccupiedByShipId);

// A crane that is lifting names the container it has hold of. That field is
// what lets the telemetry stream join the rest of the landscape: `Id` is a
// small integer, and a small integer appearing in two payloads is a
// coincidence, not evidence — the correlation analyzer rejects it, correctly.
// A container id is distinctive and already flows through the REST and GraphQL
// steps, so the crane naming one is what turns the telemetry into part of the
// same story rather than a lane of its own.
//
// Null unless Status is Lifting: an idle crane holds nothing, and inventing an
// id for it would be the same coincidence problem in a different costume.
public sealed record Crane(
    int Id,
    int DockNumber,
    decimal MaxLiftTonnes,
    CraneStatus Status,
    string? LiftingContainerId = null);

public sealed record Container(
    string Id,
    decimal WeightKg,
    string Owner,
    ContainerStatus Status,
    int? OnShipId);

public sealed record PortCall(
    int Id,
    int ShipId,
    int DockNumber,
    DateTimeOffset ScheduledArrival,
    DateTimeOffset? ActualArrival,
    DateTimeOffset? ScheduledDeparture,
    DateTimeOffset? ActualDeparture,
    PortCallStatus Status,
    CargoOperation CargoOperation,
    string? Notes);

// A live AIS position frame — the wire shape Tracking (WebSocket) emits and
// Operations (SignalR) re-broadcasts. Keyed by ShipId so a position
// correlates back to a Ship / PortCall across the landscape.
public sealed record AisPosition(
    int ShipId,
    double Latitude,
    double Longitude,
    double SpeedKnots,
    double CourseDegrees,
    DateTimeOffset At);

// A public arrivals-board event — Arrivals' (SSE) CQRS read-model projection
// of a port call reaching a milestone. Seq is the monotonic id SSE resumes
// from via Last-Event-ID.
public sealed record ArrivalEvent(
    long Seq,
    int PortCallId,
    int ShipId,
    PortCallStatus Status,
    DateTimeOffset At);
