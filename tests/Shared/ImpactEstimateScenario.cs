using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Checks the planner estimate of <c>DELETE</c>, <c>UPDATE</c> and <c>INSERT ... SELECT</c> against the real row count, and that estimating never writes.</summary>
public static class ImpactEstimateScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs the scenario against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <returns>One line per estimate with the observed figure and the real count.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password)
    {
        var d = profile.Dialect;
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var session = await manager.OpenAsync(profile, Ct);
        List<string> figures = [];
        try
        {
            foreach (var sql in Setup(d))
                await ExecAsync(session, sql);

            (string Sql, string Count)[] cases =
            [
                ("DELETE FROM ie_t", "select count(*) from ie_t"),
                ("UPDATE ie_t SET v = v + 1 WHERE v > 500", "select count(*) from ie_t where v > 500"),
                ("INSERT INTO ie_copy SELECT id, v FROM ie_t", "select count(*) from ie_t"),
            ];
            foreach (var (sql, count) in cases)
            {
                var estimate = await ImpactEstimator.EstimateAsync(session, sql, [], Ct);
                var real = await ScalarAsync(session, count);
                var line = $"{d} | {sql} | estimate {(estimate is null ? "null" : estimate.Rows)} | real {real}";
                figures.Add(line);
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "impact-figures.txt"), line + Environment.NewLine);
                // Observed on MySQL 8.4: EXPLAIN FORMAT=JSON of INSERT ... SELECT gives the plan reader no usable row figure, so the dialog shows none.
                if (d == DatabaseDialect.Sqlite || (d == DatabaseDialect.MySql && sql.StartsWith("INSERT", StringComparison.Ordinal)))
                {
                    Assert.Null(estimate);
                    continue;
                }

                Assert.NotNull(estimate);
                Assert.InRange(estimate.Rows, real / 2, real * 2);
            }

            Assert.Equal(1000, await ScalarAsync(session, "select count(*) from ie_t"));
            Assert.Equal(500500, await ScalarAsync(session, "select sum(v) from ie_t"));
            Assert.Equal(0, await ScalarAsync(session, "select count(*) from ie_copy"));
        }
        finally
        {
            await manager.CloseAsync(session);
        }

        return figures;
    }

    private static string[] Setup(DatabaseDialect d)
    {
        var type = d == DatabaseDialect.SqlServer ? "int" : "integer";
        string[] create =
        [
            $"create table ie_t (id {type} primary key, v {type} not null)",
            $"create table ie_copy (id {type} primary key, v {type} not null)",
        ];
        string[] fill = d switch
        {
            DatabaseDialect.PostgreSql => ["insert into ie_t select g, g from generate_series(1, 1000) g", "analyze ie_t", "analyze ie_copy"],
            DatabaseDialect.MySql => ["insert into ie_t with recursive s(n) as (select 1 union all select n + 1 from s where n < 1000) select n, n from s", "analyze table ie_t", "analyze table ie_copy"],
            DatabaseDialect.SqlServer => ["insert into ie_t select top 1000 row_number() over (order by (select null)), row_number() over (order by (select null)) from sys.all_objects a cross join sys.all_objects b", "update statistics ie_t", "update statistics ie_copy"],
            _ => ["insert into ie_t with recursive s(n) as (select 1 union all select n + 1 from s where n < 1000) select n, n from s"],
        };
        return [.. create, .. fill];
    }

    private static async Task ExecAsync(ConnectionSession session, string sql)
    {
        await using var reader = await session.Connection.ExecuteAsync(sql, [], Ct);
        await reader.ReadAllAsync(10, Ct);
    }

    private static async Task<long> ScalarAsync(ConnectionSession session, string sql)
    {
        await using var reader = await session.Connection.ExecuteAsync(sql, [], Ct);
        var rows = await reader.ReadAllAsync(1, Ct);
        return Convert.ToInt64(rows[0][0], System.Globalization.CultureInfo.InvariantCulture);
    }
}
