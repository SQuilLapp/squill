using Squill.Core;
using Squill.Corpus;
using Squill.Sql.Statements;

namespace Squill.Data.RoundTrip;

/// <summary>Runs every statement of a valid corpus on a real server: a statement the server rejects is a corpus bug.</summary>
internal static class SqlCorpusScenario
{
    /// <summary>Executes each entry of <c>&lt;file&gt;.valid.sql</c> between <paramref name="begin"/> and <paramref name="rollback"/>.</summary>
    /// <param name="connection">Open connection on a scratch database that holds the sample schema.</param>
    /// <param name="dialect">The engine of the connection.</param>
    /// <param name="file">Corpus file prefix.</param>
    /// <param name="begin">Statement that opens a transaction.</param>
    /// <param name="rollback">Statement that rolls it back.</param>
    /// <returns>One line per rejected entry; empty when the server accepted everything.</returns>
    public static async Task<List<string>> RunAsync(IDatabaseConnection connection, DatabaseDialect dialect, string file, string begin, string rollback)
    {
        var failures = new List<string>();
        foreach (var entry in SqlCorpus.Load(file, "valid"))
        {
            // Transaction control would nest inside the wrapping transaction: not executed.
            if (entry.Sql.StartsWith("begin", StringComparison.OrdinalIgnoreCase) || entry.Sql.StartsWith("start transaction", StringComparison.OrdinalIgnoreCase))
                continue;

            // GO and DELIMITER are client directives: the pieces the splitter finds are sent one by one, anything else as one batch (a T-SQL DECLARE must reach its SELECT).
            var pieces = HasDirective(entry.Sql)
                ? SqlStatementSplitter.Split(entry.Sql, dialect).Select(s => entry.Sql.Substring(s.Start, s.Length)).ToList()
                : [entry.Sql];
            await RunQuietlyAsync(connection, begin);
            try
            {
                foreach (var piece in pieces)
                    await MultiResultScenario.RunAsync(connection, piece);
            }
            catch (Exception e)
            {
                failures.Add($"#{entry.Index}: {e.Message.ReplaceLineEndings(" ")} <= {entry.Sql.ReplaceLineEndings(" ")}");
            }
            finally
            {
                await RunQuietlyAsync(connection, rollback);
            }
        }

        return failures;
    }

    private static bool HasDirective(string sql) =>
        sql.ReplaceLineEndings("\n").Split('\n').Any(l => l.Trim().Equals("go", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("delimiter ", StringComparison.OrdinalIgnoreCase));

    private static async Task RunQuietlyAsync(IDatabaseConnection connection, string sql)
    {
        try
        {
            await MultiResultScenario.RunAsync(connection, sql);
        }
        catch (Exception)
        {
            // nothing to roll back when the server already ended the transaction (an aborted DDL, a failed statement)
        }
    }
}
