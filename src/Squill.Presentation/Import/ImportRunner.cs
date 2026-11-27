using System.Text;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Ddl;
using Squill.Sql.Editing;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Import;

/// <summary>Inserts the rows of a CSV file into a table in batches, with progress, cancel and a rejects file.</summary>
/// <param name="dialogs">Asks the production confirmation.</param>
/// <param name="time">The clock for progress throttling, or <see langword="null"/> for the system clock.</param>
public sealed class ImportRunner(IDialogService dialogs, TimeProvider? time = null)
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>Gets the most rows one statement may carry on an engine, given the columns it writes.</summary>
    /// <param name="dialect">The engine.</param>
    /// <param name="columns">Columns per row.</param>
    /// <param name="requested">The wanted batch size.</param>
    /// <returns>The batch size, at least 1.</returns>
    public static int ClampBatchSize(DatabaseDialect dialect, int columns, int requested)
    {
        var (parameters, rows) = dialect switch
        {
            DatabaseDialect.SqlServer => (2100, 1000), // 2,100 parameters and 1,000 VALUES rows
            DatabaseDialect.Sqlite => (32766, int.MaxValue),
            DatabaseDialect.Oracle or DatabaseDialect.Informix => (65535, 1), // no multi-row VALUES: one statement per row
            _ => (65535, int.MaxValue),
        };
        return Math.Max(1, Math.Min(Math.Min(requested, rows), parameters / Math.Max(1, columns)));
    }

    /// <summary>Runs the import.</summary>
    /// <param name="job">The file, the table and the policies.</param>
    /// <param name="progress">Receives progress at most every 250 ms, and once at the end.</param>
    /// <param name="ct">Cancels the import after the current batch.</param>
    /// <returns>The figures and how it ended; failures are reported in the result, not thrown.</returns>
    public async Task<ImportResult> RunAsync(ImportJob job, IProgress<ImportProgress> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(progress);
        var run = new Run(job, _time, progress, _time.GetTimestamp());
        if (run.Active.Count == 0)
            return run.Result(ImportOutcome.Failed, UiText.Get("import.error.noColumns"));
        if (ImportValidator.GeneratedIssues(run.Active, run.Dialect).FirstOrDefault() is { } issue)
            return run.Result(ImportOutcome.Failed, issue.Message);
        if (!await ConfirmProductionAsync(job, run))
            return run.Result(ImportOutcome.Cancelled, null);

        var single = job.Options.CommitMode == ImportCommitMode.SingleTransaction;
        var stopOnError = single || job.Options.OnError == ImportErrorPolicy.Stop;
        var converter = new ImportRowConverter(job.Mapping, job.Format);
        using var operation = job.Session.Transaction.BeginOperation();
        await using var rejects = new RejectsWriter(job.Options.RejectsPath ?? Path.ChangeExtension(job.Path, ".rejets.csv"), job.Csv);
        StatementScope? whole = null;
        var outcome = ImportOutcome.Completed;
        string? error = null;
        try
        {
            if (single)
                whole = await SqlRunner.BeginScopeAsync(job.Session.Connection, run.Dialect, job.Session.Transaction, run.Tab, CancellationToken.None);
            await using var file = new FileStream(job.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
            var batch = new List<CsvRow>(run.BatchSize);
            var values = new List<object?[]>(run.BatchSize);
            var firstRow = true;
            await foreach (var row in CsvReader.ReadRowsAsync(file, job.Csv, CancellationToken.None))
            {
                if (firstRow)
                {
                    firstRow = false;
                    if (job.Csv.HasHeader)
                    {
                        rejects.Header = row.Fields;
                        continue;
                    }
                }

                // A cancel finishes the batch in memory, then stops: the current batch is never left half written.
                var cancelled = ct.IsCancellationRequested;
                if (!cancelled)
                {
                    run.Read++;
                    var converted = converter.Convert(row.Fields, row.LineNumber);
                    if (converted.Rejection is { } rejection)
                    {
                        // Under Stop the rows read before this one are written first: if one of them is bad, it is the first bad row.
                        if (stopOnError && !single && batch.Count > 0 && await FlushAsync(job, run, whole, batch, values, rejects, stopOnError) is { } earlier)
                        {
                            run.Read--;
                            (outcome, error) = (ImportOutcome.StoppedOnError, earlier);
                            break;
                        }

                        run.Rejected++;
                        await rejects.WriteAsync(row.Fields, rejection.Issue.Message);
                        if (stopOnError)
                        {
                            outcome = ImportOutcome.StoppedOnError;
                            error = rejection.Issue.Message;
                            break;
                        }

                        continue;
                    }

                    batch.Add(row);
                    values.Add(converted.Values!);
                }

                if (batch.Count >= run.BatchSize || (cancelled && batch.Count > 0))
                {
                    var stop = await FlushAsync(job, run, whole, batch, values, rejects, stopOnError);
                    batch.Clear();
                    values.Clear();
                    if (stop is not null)
                    {
                        (outcome, error) = (ImportOutcome.StoppedOnError, stop);
                        break;
                    }

                    run.Report(file, force: false);
                }

                if (cancelled)
                {
                    outcome = ImportOutcome.Cancelled;
                    break;
                }
            }

            if (outcome == ImportOutcome.Completed && batch.Count > 0 && await FlushAsync(job, run, whole, batch, values, rejects, stopOnError) is { } last)
                (outcome, error) = (ImportOutcome.StoppedOnError, last);
            run.Report(file, force: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (outcome, error) = (ImportOutcome.Failed, ex.Message);
        }

        if (whole is not null)
        {
            if (outcome == ImportOutcome.Completed)
            {
                try
                {
                    await whole.CompleteAsync(CancellationToken.None);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await whole.FailAsync();
                    (outcome, error, run.Inserted) = (ImportOutcome.Failed, ex.Message, 0);
                }
            }
            else
            {
                await whole.FailAsync();
                run.Inserted = 0;
            }
        }

        return run.Result(outcome, error, rejects.Path);
    }

    private async ValueTask<bool> ConfirmProductionAsync(ImportJob job, Run run)
    {
        var profile = job.Session.Profile;
        if (profile.Environment != ConnectionEnvironment.Production)
            return true;
        var statement = new ConfirmedStatement($"INSERT INTO {run.Table} ({run.ColumnList}) VALUES (...)", job.Target.Name, StatementKind.Insert);
        var request = new ProductionConfirmationRequest(
            ConfirmationKind.GridEdit, profile.Name, profile.Dialect == DatabaseDialect.Sqlite ? ProductionGuard.RequiredText(profile) : profile.Database ?? string.Empty,
            [statement], null, job.Target.Name, false, false,
            UiText.Format("import.confirm.title", job.Target.Name), UiText.Format("import.confirm.message", Path.GetFileName(job.Path), job.Target.Name), UiText.Get("import.confirm.button"));
        return (await dialogs.ConfirmProductionAsync(request)).Confirmed;
    }

    // Returns null when the rows were handled, else the reason the import must stop.
    private static async ValueTask<string?> FlushAsync(ImportJob job, Run run, StatementScope? whole, List<CsvRow> rows, List<object?[]> values, RejectsWriter rejects, bool stopOnError)
    {
        if (whole is not null)
        {
            try
            {
                var sql = run.Sql(values, 0, values.Count, out var parameters);
                whole.Observe(run.ShortSql(rows.Count), await SqlRunner.ExecuteAsync(job.Session.Connection, sql, parameters, CancellationToken.None));
                run.Inserted += rows.Count;
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return FirstLine(ex.Message);
            }
        }

        var rejectedBefore = run.Rejected;
        var stop = await InsertRangeAsync(job, run, rows, values, 0, rows.Count, rejects, stopOnError);
        // Every row of a multi-row batch failing means the cause is not a row (lost connection, missing table): stop with Failed.
        if (stop is null && rows.Count > 1 && run.Rejected - rejectedBefore == rows.Count)
            throw new InvalidOperationException(run.LastError ?? string.Empty);
        return stop;
    }

    // Inserts rows[lo..hi) in its own scope; on a database error halves the range until the bad rows are found.
    private static async ValueTask<string?> InsertRangeAsync(ImportJob job, Run run, List<CsvRow> rows, List<object?[]> values, int lo, int hi, RejectsWriter rejects, bool stopOnError)
    {
        var scope = await SqlRunner.BeginScopeAsync(job.Session.Connection, run.Dialect, job.Session.Transaction, run.Tab, CancellationToken.None);
        try
        {
            var sql = run.Sql(values, lo, hi, out var parameters);
            scope.Observe(run.ShortSql(hi - lo), await SqlRunner.ExecuteAsync(job.Session.Connection, sql, parameters, CancellationToken.None));
            await scope.CompleteAsync(CancellationToken.None);
            run.Inserted += hi - lo;
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await scope.FailAsync();
            if (hi - lo > 1)
            {
                var mid = lo + ((hi - lo) / 2);
                return await InsertRangeAsync(job, run, rows, values, lo, mid, rejects, stopOnError)
                    ?? await InsertRangeAsync(job, run, rows, values, mid, hi, rejects, stopOnError);
            }

            var message = FirstLine(ex.Message);
            run.LastError = message;
            run.Rejected++;
            await rejects.WriteAsync(rows[lo].Fields, message);
            return stopOnError ? message : null;
        }
    }

    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return (end < 0 ? text : text[..end]).Trim();
    }

    /// <summary>The counters and the statement builder of one import.</summary>
    private sealed class Run
    {
        private readonly TimeProvider _time;
        private readonly IProgress<ImportProgress> _progress;
        private readonly long _started;
        private long _lastReport;

        public Run(ImportJob job, TimeProvider time, IProgress<ImportProgress> progress, long started)
        {
            (_time, _progress, _started, _lastReport) = (time, progress, started, started);
            Active = [.. job.Mapping.Active];
            Dialect = job.Session.Profile.Dialect;
            BatchSize = ClampBatchSize(Dialect, Active.Count, Math.Max(1, job.Options.BatchSize));
            Table = SqlQuoting.QualifiedName(job.Target.Schema, job.Target.Name, Dialect);
            ColumnList = string.Join(", ", Active.Select(m => SqlQuoting.Quote(m.Target!.Name, Dialect)));
            Tab = UiText.Get("import.confirm.button");
        }

        public IReadOnlyList<ColumnMap> Active { get; }

        public DatabaseDialect Dialect { get; }

        public int BatchSize { get; }

        public string Table { get; }

        public string ColumnList { get; }

        public string Tab { get; }

        public long Read { get; set; }

        public long Inserted { get; set; }

        public long Rejected { get; set; }

        public string? LastError { get; set; }

        public string ShortSql(int rows) => $"INSERT INTO {Table} ({ColumnList}) VALUES ({rows} rows)";

        public string Sql(List<object?[]> values, int lo, int hi, out List<QueryParameter> parameters)
        {
            parameters = new List<QueryParameter>((hi - lo) * Active.Count);
            var text = new StringBuilder($"INSERT INTO {Table} ({ColumnList}) VALUES ");
            for (var r = lo; r < hi; r++)
            {
                text.Append(r > lo ? ", (" : "(");
                for (var c = 0; c < Active.Count; c++)
                {
                    var name = "p" + parameters.Count;
                    parameters.Add(new QueryParameter(name, values[r][c]));
                    text.Append(c > 0 ? ", @" : "@").Append(name).Append(DmlGenerator.ParameterCast(Dialect, Active[c].Target!.DataType));
                }

                text.Append(')');
            }

            return text.ToString();
        }

        public void Report(FileStream file, bool force)
        {
            var now = _time.GetTimestamp();
            if (!force && _time.GetElapsedTime(_lastReport, now) < ProgressInterval)
                return;
            _lastReport = now;
            var elapsed = _time.GetElapsedTime(_started, now);
            var handled = Inserted + Rejected;
            var rate = elapsed.TotalSeconds > 0 ? handled / elapsed.TotalSeconds : 0;
            TimeSpan? remaining = null;
            if (file.Length > 0 && file.Position > 0 && handled > 0)
                remaining = elapsed * ((double)(file.Length - file.Position) / file.Position);
            _progress.Report(new ImportProgress(Read, Inserted, Rejected, rate, remaining));
        }

        public ImportResult Result(ImportOutcome outcome, string? error, string? rejectsPath = null) =>
            new(Read, Inserted, Rejected, _time.GetElapsedTime(_started), rejectsPath, outcome, error);
    }

    /// <summary>Writes the original lines of rejected rows plus an <c>erreur</c> column, creating the file at the first rejection.</summary>
    private sealed class RejectsWriter(string target, CsvOptions csv) : IAsyncDisposable
    {
        private StreamWriter? _writer;

        public string? Path { get; private set; }

        public string[]? Header { get; set; }

        public async ValueTask WriteAsync(string[] fields, string error)
        {
            if (_writer is null)
            {
                _writer = new StreamWriter(target, false, new UTF8Encoding(true));
                Path = target;
                if (Header is { } header)
                    await _writer.WriteLineAsync(Line(header, "erreur"));
            }

            await _writer.WriteLineAsync(Line(fields, error));
        }

        public async ValueTask DisposeAsync()
        {
            if (_writer is not null)
                await _writer.DisposeAsync();
        }

        private string Line(string[] fields, string last) => string.Join(csv.Delimiter, fields.Append(last).Select(Quote));

        private string Quote(string field) =>
            field.AsSpan().IndexOfAny([csv.Delimiter, csv.Quote, '\r', '\n']) < 0
                ? field
                : csv.Quote + field.Replace(csv.Quote.ToString(), new string(csv.Quote, 2), StringComparison.Ordinal) + csv.Quote;
    }
}
