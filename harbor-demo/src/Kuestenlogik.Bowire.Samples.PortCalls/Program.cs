// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Samples.PortCalls;

// PortCalls — the port-call orchestration aggregate as a GraphQL BFF. Owns
// only the port-call record; its `ship` / `dock` / `containers` fields fan
// out to Fleet (gRPC), Inventory (OData) and Gate (REST), so one GraphQL
// query visibly resolves across three wires. A pure server; the
// Harbor.Gateway discovers it via the catalogue, or point a standalone
// workbench at graphql@http://localhost:5153/graphql.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PortCallStore>();
builder.Services.AddSingleton<FleetGateway>();
builder.Services.AddSingleton<InventoryGateway>();
builder.Services.AddSingleton<GateGateway>();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddTypeExtension<PortCallResolvers>()
    .AddInMemorySubscriptions()
    .AddHttpRequestInterceptor<IntrospectionInterceptor>();

var app = builder.Build();

app.UseWebSockets();          // subscriptions ride on WebSockets
app.MapGraphQL("/graphql");
app.MapGet("/", () => Results.Redirect("/graphql"));

app.Run();
