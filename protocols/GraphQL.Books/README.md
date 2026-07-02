# GraphQL — Books sample

HotChocolate-based GraphQL endpoint at `/graphql`. Three books in an
in-memory list, one mutation to add new ones, and a **subscription**
(`bookAdded`) that pushes each newly-added book over WebSockets — a
runnable target for Bowire's GraphQL-plugin subscription code path.

## Run

```pwsh
dotnet run --project examples/GraphQL/Books
```

Listens on `http://localhost:5183`.

## Connect from Bowire

Server URL: `http://localhost:5183/graphql`. The plugin runs the
standard introspection query and surfaces `Query.books`,
`Query.bookById`, `Mutation.addBook` and `Subscription.bookAdded`.
Subscribe to `bookAdded`, fire `addBook` in another tab, and watch the
new book arrive on the subscription stream.
