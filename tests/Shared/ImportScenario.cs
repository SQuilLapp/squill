using System.Diagnostics;
using System.Globalization;
using System.Text;
using Squill.Core;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Import;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Imports French-formatted CSV files through the real <c>ImportRunner</c> on a live connection and checks what the table holds afterwards.</summary>
public static class ImportScenario
{
    private const int Rows = 5000;
    private const int BadRows = 20;
    private const int BigRows = 100_000;
    private static readonly CsvOptions Csv = new(new UTF8Encoding(false), ';', '"', true);
    private static readonly ImportFormat French = new() { DecimalSeparator = ',', ThousandsSeparator = ' ', DateFormat = "dd/MM/yyyy" };
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every import case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <returns>How long the 100,000-row import took.</returns>
    public static async Task<TimeSpan> RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password)
    {
        var d = profile.Dialect;
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var manager = new SessionManager(registry, new ScenarioProfileStore(password), new ScenarioTunnelFactory());
        var session = await manager.OpenAsync(profile, Ct);
        var dir = Directory.CreateTempSubdirectory("squill-import-scenario-").FullName;
        try
        {
            var schema = d switch { DatabaseDialect.Sqlite => "main", DatabaseDialect.PostgreSql => "public", DatabaseDialect.SqlServer => "dbo", _ => profile.Database };
            var name = SqlQuoting.QualifiedName(schema, "imp_orders", d);
            await ExecAsync(session, $"DROP TABLE IF EXISTS {name}");
            var check = d == DatabaseDialect.Sqlite ? " CHECK (length(label) <= 10)" : string.Empty; // SQLite does not enforce varchar(n)
            await ExecAsync(session, $"CREATE TABLE {name} (id int NOT NULL PRIMARY KEY, label varchar(10) NOT NULL{check}, amount decimal(10,2) NULL, due date NULL)");
            await session.RefreshSchemaAsync(Ct);
            var table = session.Schema.Tables.First(t => string.Equals(t.Name, "imp_orders", StringComparison.OrdinalIgnoreCase));
            var runner = new ImportRunner(new Dialogs());
            var file = Path.Combine(dir, "commandes_2026.csv");
            WriteCsv(file, Rows, bad: true);

            // Reject: the good rows land, each bad one goes to the rejects file with its error.
            var result = await runner.RunAsync(Job(session, table, file, new ImportOptions()), new Progress(), Ct);
            Assert.Equal(ImportOutcome.Completed, result.Completed);
            Assert.Equal((Rows, Rows - BadRows, BadRows), (result.Read, result.Inserted, result.Rejected));
            Assert.Equal(BadRows + 1, File.ReadAllLines(result.RejectsPath!).Length);
            Assert.Equal([(Rows - BadRows).ToString(CultureInfo.InvariantCulture)], await QueryAsync(session, $"SELECT COUNT(*) FROM {name}"));
            // French decimals and dates, thousands separator and accents survive.
            Assert.Equal(["1"], await QueryAsync(session, $"SELECT COUNT(*) FROM {name} WHERE id = 1 AND label = 'Éloïse' AND amount = 1.25 AND due = '2026-03-05'"));
            Assert.Equal(["1"], await QueryAsync(session, $"SELECT COUNT(*) FROM {name} WHERE id = 2 AND amount = 1234.56"));

            // Stop: ends at the first bad row (id 250); earlier rows stay.
            await ExecAsync(session, $"DELETE FROM {name}");
            result = await runner.RunAsync(Job(session, table, file, new ImportOptions { OnError = ImportErrorPolicy.Stop }), new Progress(), Ct);
            Assert.Equal(ImportOutcome.StoppedOnError, result.Completed);
            Assert.Equal((249, 1), (result.Inserted, result.Rejected));
            Assert.Equal(["249"], await QueryAsync(session, $"SELECT COUNT(*) FROM {name}"));

            // SingleTransaction: a bad row leaves the table untouched.
            await ExecAsync(session, $"DELETE FROM {name}");
            result = await runner.RunAsync(Job(session, table, file, new ImportOptions { CommitMode = ImportCommitMode.SingleTransaction }), new Progress(), Ct);
            Assert.Equal(ImportOutcome.StoppedOnError, result.Completed);
            Assert.Equal(["0"], await QueryAsync(session, $"SELECT COUNT(*) FROM {name}"));

            // A big clean file in a bounded time.
            await ExecAsync(session, $"DELETE FROM {name}");
            var big = Path.Combine(dir, "big.csv");
            WriteCsv(big, BigRows, bad: false);
            var clock = Stopwatch.StartNew();
            result = await runner.RunAsync(Job(session, table, big, new ImportOptions()), new Progress(), Ct);
            clock.Stop();
            Assert.Equal((ImportOutcome.Completed, BigRows, 0), (result.Completed, (int)result.Inserted, (int)result.Rejected));
            Assert.Equal([BigRows.ToString(CultureInfo.InvariantCulture)], await QueryAsync(session, $"SELECT COUNT(*) FROM {name}"));
            await ExecAsync(session, $"DROP TABLE {name}");
            return clock.Elapsed;
        }
        finally
        {
            await manager.CloseAsync(session);
            Directory.Delete(dir, true);
        }
    }

    private static ImportJob Job(ConnectionSession session, TableInfo table, string file, ImportOptions options) =>
        new(session, table, ImportMapper.AutoMap(["id", "label", "amount", "due"], table.Columns), French, Csv, file, options);

    // Every 250th row of a bad file is wrong: a bad number, a text too long for the column, or an empty required value.
    private static void WriteCsv(string path, int rows, bool bad)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("id;label;amount;due");
        for (var i = 1; i <= rows; i++)
        {
            var amount = i % 2 == 0 ? "1 234,56" : "1,25";
            var label = "Éloïse";
            if (bad && i % 250 == 0)
            {
                switch ((i / 250) % 3)
                {
                    case 0: amount = "abc"; break;
                    case 1: label = "far too long for ten"; break;
                    default: label = string.Empty; break;
                }
            }

            writer.WriteLine($"{i};{label};{amount};05/03/2026");
        }
    }

    private static async Task ExecAsync(ConnectionSession session, string sql)
    {
        await using var reader = await session.Connection.ExecuteAsync(sql, [], Ct);
        await reader.ReadAllAsync(10, Ct);
    }

    private static async Task<string[]> QueryAsync(ConnectionSession session, string sql)
    {
        await using var reader = await session.Connection.ExecuteAsync(sql, [], Ct);
        var rows = await reader.ReadAllAsync(100, Ct);
        return [.. rows.Select(r => string.Join('|', r.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture))))];
    }

    private sealed class Progress : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value)
        {
        }
    }

    private sealed class Dialogs : IDialogService
    {
        public ValueTask<bool> ConfirmSshHostKeyAsync(Squill.Platform.Ssh.SshHostKeyRequest request) => ValueTask.FromResult(false);

        public ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
            ValueTask.FromResult(new Squill.Presentation.Safety.ProductionConfirmationResult(true, false));

        public ValueTask<Squill.Presentation.Safety.TransactionChoice> AskTransactionAsync(Squill.Presentation.Safety.TransactionCloseRequest request) =>
            ValueTask.FromResult(Squill.Presentation.Safety.TransactionChoice.Rollback);

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);

        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<Squill.Presentation.Query.ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, Squill.Presentation.Query.ParameterValue> defaults) =>
            ValueTask.FromResult<IReadOnlyList<Squill.Presentation.Query.ParameterValue>?>(null);
    }
}
