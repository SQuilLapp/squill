using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>Every statement of <c>informix.valid.sql</c> must be accepted by a real Informix server.</summary>
public sealed class SqlCorpusTests(InformixFixture server) : IClassFixture<InformixFixture>
{
    private static readonly string[] SampleSchema =
    [
        "create table customers (id serial not null primary key, name varchar(80) not null, email varchar(120), created_at datetime year to second)",
        "create table orders (id serial not null primary key, customer_id integer not null, placed_on date, total decimal(10,2))",
        "create table order_items (order_id integer not null, line_no integer not null, sku varchar(40), quantity integer, primary key (order_id, line_no))",
        "create table no_pk (label varchar(40), amount integer)",
        "create view order_totals (customer_id, name, orders, total) as select c.id, c.name, count(o.id), sum(o.total) from customers c, outer orders o where o.customer_id = c.id group by c.id, c.name",
    ];

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryValidStatement_IsAcceptedByTheServer()
    {
        Assert.SkipWhen(InformixFixture.FindClidriver() is null, "IBM clidriver not found: set SQUILL_CLIDRIVER or restore the Net.IBM.Data.Db2 package (see InformixFixture).");
        await using var connection = await new InformixDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "corpus", DatabaseDialect.Informix, InformixFixture.Host, server.Port, InformixFixture.CorpusDatabase, InformixFixture.UserName, null, null),
            InformixFixture.Password,
            CancellationToken.None);
        foreach (var statement in SampleSchema)
            await MultiResultScenario.RunAsync(connection, statement);

        var failures = await SqlCorpusScenario.RunAsync(connection, DatabaseDialect.Informix, "informix", "begin", "rollback");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
