# Kuestenlogik.Bowire.Samples.GraphQL

Isolated **GraphQL-only** sample on `https://localhost:5115/bowire` (schema at `/graphql`).

## What this demonstrates

- **Query** — `ships`, `ship(id)`, `docks`, `portCalls(status)`
- **Mutation** — `schedulePortCall`, `updatePortCallStatus`
- **Subscription** — `onPortCallChanged` over WebSocket
- **Deep nested resolvers** — query `ships { portCalls { dock { cranes } } }` in one round-trip
- **Resolver extensions** — `ShipResolvers` + `DockResolvers` add dynamic fields via `[ExtendObjectType]`
- **Schema introspection** — browse the graph with `{ __schema { types { name } } }`

Built on HotChocolate 15 with in-memory subscriptions.

## Minimum viable setup

```csharp
builder.Services.AddBowire();
builder.Services.AddGraphQLServer()
    .AddQueryType<MyQuery>()
    .AddInMemorySubscriptions();

app.UseWebSockets();
app.MapGraphQL("/graphql");
app.MapBowire();
```
