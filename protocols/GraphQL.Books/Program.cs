// GraphQL Books sample for the Bowire GraphQL plugin demo. HotChocolate
// publishes the schema at /graphql which Bowire fetches via the standard
// introspection query. Query + Mutation + Subscription — the subscription
// gives Bowire's GraphQL plugin a runnable subscription target
// (Bowire.Samples #13).

using HotChocolate.Subscriptions;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5183");
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions();

var app = builder.Build();
app.UseWebSockets();          // subscriptions ride on WebSockets
app.MapGraphQL();
await app.RunAsync();

sealed class Query
{
    private static readonly List<Book> s_books = new()
    {
        new(1, "The Pragmatic Programmer", "Andrew Hunt"),
        new(2, "Domain-Driven Design",      "Eric Evans"),
        new(3, "Refactoring",               "Martin Fowler"),
    };

    public IEnumerable<Book> Books() => s_books;
    public Book? BookById(int id) => s_books.FirstOrDefault(b => b.Id == id);
    internal static List<Book> All => s_books;
}

sealed class Mutation
{
    // Adds a book and publishes it to the `bookAdded` subscription stream.
    // ITopicEventSender is registered by AddInMemorySubscriptions and
    // injected into the resolver automatically.
    public async Task<Book> AddBook(string title, string author, ITopicEventSender sender)
    {
        var book = new Book(Query.All.Count + 1, title, author);
        Query.All.Add(book);
        await sender.SendAsync(nameof(Subscription.BookAdded), book);
        return book;
    }
}

sealed class Subscription
{
    // subscription { bookAdded { id title author } } — pushes every
    // newly-added book to connected clients over the WebSocket transport.
    [Subscribe]
    public Book BookAdded([EventMessage] Book book) => book;
}

record Book(int Id, string Title, string Author);
