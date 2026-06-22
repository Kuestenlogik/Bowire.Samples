// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kuestenlogik.Bowire.Samples.Shared;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;

// GraphQL is discovered externally: Bowire introspects the schema via
// HTTP + `__schema` queries, so this sample only hosts the GraphQL
// endpoint itself. Browse it with a standalone Bowire:
//   bowire --url https://localhost:5115/graphql

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(HarborStore.CreateSeeded());

// HotChocolate — Query + Mutation + Subscription in one graph. The
// in-memory pub/sub transport is fine for a sample; Redis is the
// production pattern.
builder.Services
    .AddGraphQLServer()
    .AddQueryType<HarborQuery>()
    .AddMutationType<HarborMutation>()
    .AddSubscriptionType<HarborSubscription>()
    .AddInMemorySubscriptions()
    // HotChocolate 15 blocks introspection by default when no request
    // interceptor explicitly allows it. Bowire discovers the schema via
    // `__schema` queries, so unconditionally allow it for this demo.
    .AddHttpRequestInterceptor<IntrospectionInterceptor>();

var app = builder.Build();

// Forward HarborStore events into the GraphQL subscription stream.
app.Lifetime.ApplicationStarted.Register(async () =>
{
    var store = app.Services.GetRequiredService<HarborStore>();
    var sender = app.Services.GetRequiredService<ITopicEventSender>();
    store.PortCallChanged += pc => _ = sender.SendAsync("OnPortCallChanged", pc);
    await Task.CompletedTask;
});

app.UseWebSockets();          // subscriptions ride on WS
app.MapGraphQL("/graphql");   // GET /graphql → Banana-Cake-Pop UI

app.Run();

// -----------------------------------------------------------
// Introspection interceptor — see AddGraphQLServer config above.
// -----------------------------------------------------------
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

// -----------------------------------------------------------
// Schema
// -----------------------------------------------------------
public sealed class HarborQuery
{
    /// <summary>Every ship. Deep-nested expansions (`ship → portCalls → dock → cranes`) drop out of the resolver graph automatically.</summary>
    public IEnumerable<Ship> Ships([Service] HarborStore store) => store.Ships.Values;

    public Ship? Ship(int id, [Service] HarborStore store)
        => store.Ships.TryGetValue(id, out var s) ? s : null;

    public IEnumerable<Dock> Docks([Service] HarborStore store) => store.Docks.Values;

    public IEnumerable<PortCall> PortCalls(
        [Service] HarborStore store,
        PortCallStatus? status = null)
        => status is { } s
            ? store.PortCalls.Values.Where(pc => pc.Status == s)
            : store.PortCalls.Values;
}

/// <summary>Extend the <see cref="Ship"/> type with a dynamic field that resolves
/// related port calls. HotChocolate wires this automatically via the
/// <see cref="ExtendObjectTypeAttribute"/>.</summary>
[ExtendObjectType(typeof(Ship))]
public sealed class ShipResolvers
{
    public IEnumerable<PortCall> PortCalls([Parent] Ship ship, [Service] HarborStore store)
        => store.PortCalls.Values.Where(pc => pc.ShipId == ship.Id);
}

[ExtendObjectType(typeof(Dock))]
public sealed class DockResolvers
{
    public IEnumerable<Crane> Cranes([Parent] Dock dock, [Service] HarborStore store)
        => store.Cranes.Values.Where(c => c.DockNumber == dock.Number);
}

public sealed class HarborMutation
{
    public PortCall SchedulePortCall(
        int shipId, int dockNumber, DateTimeOffset scheduledArrival,
        CargoOperation cargoOperation,
        [Service] HarborStore store)
    {
        var id = store.NextPortCallId();
        var pc = new PortCall(
            Id: id, ShipId: shipId, DockNumber: dockNumber,
            ScheduledArrival: scheduledArrival,
            ActualArrival: null, ScheduledDeparture: null, ActualDeparture: null,
            Status: PortCallStatus.Scheduled,
            CargoOperation: cargoOperation, Notes: null);
        store.PortCalls[id] = pc;
        store.RaisePortCallChanged(pc);
        return pc;
    }

    public PortCall? UpdatePortCallStatus(int id, PortCallStatus status, [Service] HarborStore store)
    {
        if (!store.PortCalls.TryGetValue(id, out var pc)) return null;
        var updated = pc with { Status = status };
        store.PortCalls[id] = updated;
        store.RaisePortCallChanged(updated);
        return updated;
    }
}

public sealed class HarborSubscription
{
    /// <summary>Pushes every port-call change to subscribers.</summary>
    [Subscribe]
    [Topic("OnPortCallChanged")]
    public PortCall OnPortCallChanged([EventMessage] PortCall pc) => pc;
}
