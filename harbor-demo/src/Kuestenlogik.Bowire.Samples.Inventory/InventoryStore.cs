// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Inventory;

/// <summary>
/// Inventory's own private store — the physical harbor assets it owns:
/// docks and static crane configuration, seeded from <see cref="HarborSeed"/>.
/// (Live crane <em>status</em> belongs to the Telemetry service, keyed by the
/// same CraneId — the shared-kernel split from harbor-demo/REDESIGN.md.)
/// </summary>
public sealed class InventoryStore
{
    public IReadOnlyList<Dock> Docks { get; } = HarborSeed.Docks();
    public IReadOnlyList<Crane> Cranes { get; } = HarborSeed.Cranes();
}
