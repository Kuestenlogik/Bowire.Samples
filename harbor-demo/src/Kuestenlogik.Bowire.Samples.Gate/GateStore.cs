// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.Gate;

/// <summary>
/// Gate's own private store — the containers it owns through their yard
/// lifecycle (gate-in → load → discharge → gate-out), seeded from
/// <see cref="HarborSeed"/>. Mutable, unlike Inventory's read-only assets.
/// A container references the ship it sits on by <c>OnShipId</c> only — the
/// ship record itself is Fleet's (cross-context by id).
/// </summary>
public sealed class GateStore
{
    private readonly Dictionary<string, Container> _containers =
        HarborSeed.Containers().ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Container> All => _containers.Values;
    public Container? Find(string id) => _containers.TryGetValue(id, out var c) ? c : null;
    public bool Exists(string id) => _containers.ContainsKey(id);
    public void Put(Container c) => _containers[c.Id] = c;
    public bool Remove(string id) => _containers.Remove(id);
}
