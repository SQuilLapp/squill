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
    public bool SupportsActualPlan => true;

    /// <inheritdoc />
    public async ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        if (mode == PlanMode.Actual)
            return await ActualAsync(sql, parameters, cancellationToken);
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

    // Runs the (read-only) statement with row-source statistics on, then reads them back from the cursor the session ran last.
    private async ValueTask<QueryPlan> ActualAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        await (await ExecuteAsync("alter session set statistics_level = all", [], cancellationToken)).DisposeAsync();
        try
        {
            await using (var run = await ExecuteAsync(sql, parameters, cancellationToken))
            {
                while (await run.ReadBatchAsync(1000, cancellationToken) is not null)
                {
                }
            }

            try
            {
                await using var plan = await ExecuteAsync(
                    "select id, parent_id, operation, options, object_owner, object_name, cardinality, cost, bytes, to_char(substr(access_predicates, 1, 1000)), to_char(substr(filter_predicates, 1, 1000)), " +
                    "last_output_rows, last_elapsed_time, last_starts from v$sql_plan_statistics_all " +
                    "where (sql_id, child_number) = (select prev_sql_id, prev_child_number from v$session where sid = sys_context('USERENV', 'SID')) order by id", [], cancellationToken);
                var rows = (await plan.ReadAllAsync(256, cancellationToken)).Select(r => new OraclePlanRow(
                    Convert.ToInt64(r[0], CultureInfo.InvariantCulture), r[1] is null ? null : Convert.ToInt64(r[1], CultureInfo.InvariantCulture),
                    Convert.ToString(r[2], CultureInfo.InvariantCulture) ?? "", r[3] as string, r[4] as string, r[5] as string,
                    Number(r[6]), Number(r[7]), Number(r[8]), r[9] as string, r[10] as string, Number(r[11]) ?? 0, Number(r[12]), Number(r[13]))).ToList();
                if (rows.Count == 0)
                    throw new PlanUnavailableException("engine", "Oracle kept no execution statistics for this statement.");
                return OraclePlanParser.Parse(sql, rows, null, PlanMode.Actual);
            }
            catch (OracleBridgeException ex)
            {
                throw new PlanUnavailableException("engine", "Reading V$SQL_PLAN_STATISTICS_ALL needs the SELECT_CATALOG_ROLE role: " + ex.Message);
            }
        }
        finally
        {
            await (await ExecuteAsync("alter session set statistics_level = typical", [], CancellationToken.None)).DisposeAsync();
        }
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
