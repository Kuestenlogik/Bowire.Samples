// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using Kuestenlogik.Bowire.Samples.Shared;

namespace Kuestenlogik.Bowire.Samples.PortCalls;

public sealed class Query
{
    public IEnumerable<PortCall> PortCalls([Service] PortCallStore store) => store.All;

    public PortCall? PortCall(int id, [Service] PortCallStore store) => store.Find(id);
}

/// <summary>
/// The BFF fan-out. HotChocolate wires these onto the <see cref="PortCall"/>
/// type, so a single <c>portCall(id) { ship {…} dock {…} containers {…} }</c>
/// query resolves the port call from PortCalls' own store, the ship from
/// Fleet over gRPC, the dock from Inventory over OData, and the containers
/// from Gate over REST — one query, three wires.
/// </summary>
[ExtendObjectType(typeof(PortCall))]
public sealed class PortCallResolvers
{
    public Task<Ship?> GetShip(
        [Parent] PortCall portCall, [Service] FleetGateway fleet, CancellationToken ct)
        => fleet.GetShipAsync(portCall.ShipId, ct);

    public Task<Dock?> GetDock(
        [Parent] PortCall portCall, [Service] InventoryGateway inventory, CancellationToken ct)
        => inventory.GetDockAsync(portCall.DockNumber, ct);

    public Task<IReadOnlyList<Container>> GetContainers(
        [Parent] PortCall portCall, [Service] GateGateway gate, CancellationToken ct)
        => gate.GetContainersOnShipAsync(portCall.ShipId, ct);
}

public sealed class Mutation
{
    /// <summary>
    /// Advance the port-call state machine one step and publish the change to
    /// the <c>onPortCallChanged</c> subscription.
    /// </summary>
    public async Task<PortCall?> AdvancePortCall(
        int id, [Service] PortCallStore store, [Service] ITopicEventSender sender)
    {
        var updated = store.Advance(id);
        if (updated is not null)
            await sender.SendAsync("OnPortCallChanged", updated);
        return updated;
    }
}

public sealed class Subscription
{
    [Subscribe]
    [Topic("OnPortCallChanged")]
    public PortCall OnPortCallChanged([EventMessage] PortCall portCall) => portCall;
}

/// <summary>
/// HotChocolate 16 blocks introspection by default. Bowire discovers the
/// schema via <c>__schema</c> queries, so allow it unconditionally for this
/// demo (same as the harbor GraphQL sample).
/// </summary>
public sealed class IntrospectionInterceptor : DefaultHttpRequestInterceptor
{
    public override ValueTask OnCreateAsync(
        HttpContext context,
        IRequestExecutor requestExecutor,
        OperationRequestBuilder requestBuilder,
        CancellationToken cancellationToken)
    {
        requestBuilder.AllowIntrospection();
        return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }
}
