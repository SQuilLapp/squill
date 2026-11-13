using System.Globalization;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Oracle;

/// <summary>An Oracle connection held by a bridge process.</summary>
internal sealed class OracleDatabaseConnection(BridgeClient client, string serverVersion) : IDatabaseConnection, IPlanReader
{
    /// <inheritdoc />
    public string ServerVersion { get; } = serverVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(new BridgeRequest(
            0, "execute",
            Sql: sql,
            Parameters: [.. parameters.Select(p => new WireParameter(p.Name, WireValue.Encode(p.Value)))]), cancellationToken);
        return new OracleResultReader(client, response);
    }

    /// <inheritdoc />
    public bool SupportsActualPlan => false;

    /// <inheritdoc />
    public async ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        if (mode == PlanMode.Actual)
            throw new PlanUnavailableException("engine", "Oracle plans here are estimates: the statement is not executed.");
        var id = "SQ" + Guid.NewGuid().ToString("N")[..28];
        var statement = sql.TrimEnd().TrimEnd(';');
        await (await ExecuteAsync($"explain plan set statement_id = '{id}' for {statement}", parameters, cancellationToken)).DisposeAsync();

        await using var plan = await ExecuteAsync(
            "select id, parent_id, operation, options, object_owner, object_name, cardinality, cost, bytes, to_char(substr(access_predicates, 1, 1000)), to_char(substr(filter_predicates, 1, 1000)) " +
            $"from plan_table where statement_id = '{id}' order by id", [], cancellationToken);
        var rows = (await plan.ReadAllAsync(256, cancellationToken)).Select(r => new OraclePlanRow(
            Convert.ToInt64(r[0], CultureInfo.InvariantCulture), r[1] is null ? null : Convert.ToInt64(r[1], CultureInfo.InvariantCulture),
            Convert.ToString(r[2], CultureInfo.InvariantCulture) ?? "", r[3] as string, r[4] as string, r[5] as string,
            Number(r[6]), Number(r[7]), Number(r[8]), r[9] as string, r[10] as string)).ToList();

        string? text = null;
        try
        {
            await using var display = await ExecuteAsync($"select plan_table_output from table(dbms_xplan.display('PLAN_TABLE', '{id}', 'TYPICAL'))", [], cancellationToken);
            text = string.Join('\n', (await display.ReadAllAsync(256, cancellationToken)).Select(r => r[0] as string));
        }
        catch (OracleBridgeException)
        {
            // Without DBMS_XPLAN the text view draws the tree from the rows.
        }

        return OraclePlanParser.Parse(sql, rows, text);
    }

    private static double? Number(object? value) => value is null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        OracleSchemaReader.ReadAsync(this, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        // Oracle has no cross-database browsing here: the "databases" are the schemas (users) of the connected service.
        var filter = OracleSchemaReader.UserFilter("username");
        await using var reader = await ExecuteAsync($"select username from all_users where {filter} order by username", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => client.DisposeAsync();
}
