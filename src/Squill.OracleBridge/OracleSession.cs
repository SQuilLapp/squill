using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Squill.Data.Oracle;

namespace Squill.OracleBridge;

/// <summary>The state of one bridged connection: the open Oracle connection, its local transaction and at most one open reader.</summary>
internal sealed class OracleSession : IAsyncDisposable
{
    private const int MaxMessageLines = 5000;

    private OracleConnection? _connection;
    private OracleDataReader? _reader;
    private volatile OracleCommand? _command;
    private OracleTransaction? _transaction;
    private int _commandTimeout;
    private bool _outputEnabled;

    /// <summary>Interrupts the statement that is running (called from the reader thread while a request is in flight); the request then fails with <c>ORA-01013</c>.</summary>
    public void Cancel()
    {
        try
        {
            _command?.Cancel();
        }
        catch (Exception e) when (e is OracleException or InvalidOperationException or ObjectDisposedException)
        {
            // Nothing to interrupt any more.
        }
    }

    /// <summary>Runs one request; every failure becomes an error response so the loop keeps going.</summary>
    public async Task<BridgeResponse> HandleAsync(BridgeRequest request)
    {
        try
        {
            return request.Op switch
            {
                "open" => await OpenAsync(request),
                "execute" => await ExecuteAsync(request),
                "fetch" => await FetchAsync(request),
                "nextResult" => await NextResultAsync(request),
                "begin" => Begin(request),
                "commit" => Finish(request, commit: true),
                "rollback" => request.Name is null ? Finish(request, commit: false) : RollbackToSavepoint(request),
                "savepoint" => Savepoint(request),
                "probe" => await ProbeAsync(request),
                "closeReader" or "close" => await CloseReaderAsync(request),
                _ => new BridgeResponse(request.Id, false, $"Unknown operation '{request.Op}'."),
            };
        }
        catch (Exception e)
        {
            await ReleaseReaderAsync();
            return new BridgeResponse(request.Id, false, e.Message);
        }
    }

    private async Task<BridgeResponse> OpenAsync(BridgeRequest request)
    {
        var builder = new OracleConnectionStringBuilder
        {
            DataSource = request.Descriptor ?? $"{request.Host}:{request.Port}/{request.Service}",
            UserID = request.User,
            Password = request.Password,
            ConnectionTimeout = request.ConnectTimeoutSeconds ?? 15,
        };
        _commandTimeout = request.CommandTimeoutSeconds ?? 0;
        if (request.WalletPath is { Length: > 0 } wallet)
        {
            OracleConfiguration.TnsAdmin = wallet;
            OracleConfiguration.WalletLocation = wallet;
        }

        _connection = new OracleConnection(builder.ConnectionString);
        await _connection.OpenAsync();
        try
        {
            await using var enable = _connection.CreateCommand();
            enable.CommandText = "begin dbms_output.enable(null); end;";
            await enable.ExecuteNonQueryAsync();
            _outputEnabled = true;
        }
        catch (OracleException)
        {
            // No EXECUTE on DBMS_OUTPUT: the Messages tab stays empty.
        }
        return new BridgeResponse(request.Id, true, ServerVersion: _connection.ServerVersion);
    }

    private async Task<BridgeResponse> ExecuteAsync(BridgeRequest request)
    {
        if (_connection is null)
            throw new InvalidOperationException("The connection is not open.");
        await ReleaseReaderAsync();

        _command = _connection.CreateCommand();
        _command.CommandText = Normalize(request.Sql ?? "");
        _command.BindByName = true;
        _command.InitialLONGFetchSize = -1; // whole LONG values (data_default, view text), not just the length
        _command.CommandTimeout = _commandTimeout;
        _command.Transaction = _transaction;
        foreach (var p in request.Parameters ?? [])
        {
            _command.Parameters.Add(Bind(p.Name.TrimStart(':', '@'), WireValue.Decode(p.Value)));
        }

        var outs = request.OutParameters ?? [];
        if (outs.Length > 0)
        {
            foreach (var o in outs)
                _command.Parameters.Add(new OracleParameter(o.Name, o.Kind == "s" ? OracleDbType.Varchar2 : o.Kind == "x" ? OracleDbType.Raw : OracleDbType.Decimal) { Direction = ParameterDirection.Output, Size = o.Kind == "x" ? 2000 : 4000 });
            var changed = await _command.ExecuteNonQueryAsync();
            var values = outs.Select(o => OutCell(_command.Parameters[o.Name].Value)).ToArray();
            await ReleaseReaderAsync();
            return new BridgeResponse(request.Id, true, Columns: [], Done: true, RecordsAffected: changed, Messages: await DrainMessagesAsync(), OutValues: values);
        }

        _reader = (OracleDataReader)await _command.ExecuteReaderAsync();
        var columns = ColumnsOf(_reader);
        if (columns.Length > 0)
            return new BridgeResponse(request.Id, true, Columns: columns, Messages: await DrainMessagesAsync());

        var affected = _reader.RecordsAffected;
        await ReleaseReaderAsync();
        return new BridgeResponse(request.Id, true, Columns: columns, Done: true, RecordsAffected: affected, Messages: await DrainMessagesAsync());
    }

    // ODP.NET has no BOOLEAN bind before 23ai (1/0 is sent); a DateTime keeps its fraction as a TIMESTAMP; a DateTimeOffset keeps its offset as TIMESTAMP WITH TIME ZONE.
    private static OracleParameter Bind(string name, object? value) => value switch
    {
        bool b => new OracleParameter(name, b ? 1 : 0),
        DateTime t => new OracleParameter(name, OracleDbType.TimeStamp) { Value = t },
        DateTimeOffset o => new OracleParameter(name, OracleDbType.TimeStampTZ) { Value = new OracleTimeStampTZ(o.DateTime, o.ToString("zzz", System.Globalization.CultureInfo.InvariantCulture)) },
        _ => new OracleParameter(name, value ?? DBNull.Value),
    };

    // The value of an output parameter: a NUMBER the decimal type cannot hold travels as its text.
    private static WireCell? OutCell(object? value) => value switch
    {
        OracleDecimal { IsNull: true } or OracleString { IsNull: true } or OracleBinary { IsNull: true } => null,
        OracleDecimal d => d.CompareTo(new OracleDecimal(decimal.MaxValue)) > 0 || d.CompareTo(new OracleDecimal(decimal.MinValue)) < 0 ? new WireCell("s", d.ToString()) : WireValue.Encode(d.Value),
        OracleString s => WireValue.Encode(s.Value),
        OracleBinary b => WireValue.Encode(b.Value),
        _ => WireValue.Encode(value),
    };

    private static bool IsTimeStampTz(OracleDataReader reader, int i) => reader.GetDataTypeName(i) == "TimeStampTZ";

    private static WireColumn[] ColumnsOf(OracleDataReader reader) =>
        [.. Enumerable.Range(0, reader.FieldCount).Select(i => new WireColumn(reader.GetName(i), reader.GetDataTypeName(i), IsTimeStampTz(reader, i) ? "o" : WireValue.KindOf(reader.GetFieldType(i))))];

    // The reader stays open after its last row: implicit results (DBMS_SQL.RETURN_RESULT) and REF CURSORs follow through nextResult.
    private async Task<BridgeResponse> NextResultAsync(BridgeRequest request)
    {
        if (_reader is null)
            return new BridgeResponse(request.Id, true, Done: true);
        if (await _reader.NextResultAsync())
            return new BridgeResponse(request.Id, true, Columns: ColumnsOf(_reader), Messages: await DrainMessagesAsync());
        var affected = _reader.RecordsAffected;
        await ReleaseReaderAsync();
        return new BridgeResponse(request.Id, true, Done: true, RecordsAffected: affected, Messages: await DrainMessagesAsync());
    }

    private BridgeResponse Begin(BridgeRequest request)
    {
        // A transaction left over by an implicit DDL commit is simply reused.
        _transaction ??= (_connection ?? throw new InvalidOperationException("The connection is not open.")).BeginTransaction();
        return new BridgeResponse(request.Id, true, Done: true);
    }

    private BridgeResponse Finish(BridgeRequest request, bool commit)
    {
        var transaction = _transaction;
        _transaction = null;
        if (commit)
            transaction?.Commit();
        else
            transaction?.Rollback();
        transaction?.Dispose();
        return new BridgeResponse(request.Id, true, Done: true);
    }

    private BridgeResponse Savepoint(BridgeRequest request)
    {
        (_transaction ?? throw new InvalidOperationException("No transaction is open.")).Save(request.Name ?? "");
        return new BridgeResponse(request.Id, true, Done: true);
    }

    private BridgeResponse RollbackToSavepoint(BridgeRequest request)
    {
        (_transaction ?? throw new InvalidOperationException("No transaction is open.")).Rollback(request.Name!);
        return new BridgeResponse(request.Id, true, Done: true);
    }

    private async Task<BridgeResponse> ProbeAsync(BridgeRequest request)
    {
        if (_connection is null)
            throw new InvalidOperationException("The connection is not open.");
        await using var command = _connection.CreateCommand();
        command.CommandText = "select dbms_transaction.local_transaction_id from dual";
        return new BridgeResponse(request.Id, true, InTransaction: await command.ExecuteScalarAsync() is not (null or DBNull));
    }

    // Reads what DBMS_OUTPUT.PUT_LINE buffered since the last call; null when there is nothing (or the package is unavailable).
    private async Task<string[]?> DrainMessagesAsync()
    {
        if (!_outputEnabled || _connection is null)
            return null;
        var lines = new List<string>();
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = "begin dbms_output.get_line(:line, :status); end;";
            command.BindByName = true;
            var line = new OracleParameter("line", OracleDbType.Varchar2, 32767, null, ParameterDirection.Output);
            var status = new OracleParameter("status", OracleDbType.Int32, ParameterDirection.Output);
            command.Parameters.Add(line);
            command.Parameters.Add(status);
            while (lines.Count < MaxMessageLines)
            {
                await command.ExecuteNonQueryAsync();
                if (status.Value is not OracleDecimal { IsNull: false } code || code.ToInt32() != 0)
                    break;
                lines.Add(line.Value is OracleString { IsNull: false } text ? text.Value : "");
            }
        }
        catch (OracleException)
        {
            // Return what was read.
        }
        return lines.Count == 0 ? null : [.. lines];
    }

    private async Task<BridgeResponse> FetchAsync(BridgeRequest request)
    {
        var maxRows = request.MaxRows ?? 100;
        if (_reader is null)
            return new BridgeResponse(request.Id, true, Done: true);
        var rows = new List<WireCell?[]>();
        var done = false;
        while (rows.Count < maxRows)
        {
            if (!await _reader.ReadAsync())
            {
                done = true;
                break;
            }
            var row = new WireCell?[_reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = _reader.IsDBNull(i) ? null : ReadCell(i);
            rows.Add(row);
        }
        var affected = done ? _reader.RecordsAffected : -1;
        return new BridgeResponse(request.Id, true, Rows: [.. rows], Done: done, RecordsAffected: affected, Messages: done ? await DrainMessagesAsync() : null);
    }

    // NUMBER values beyond decimal's range, and exotic types, fall back to their text form.
    private WireCell? ReadCell(int i)
    {
        try
        {
            if (IsTimeStampTz(_reader!, i))
            {
                var stamp = _reader!.GetOracleTimeStampTZ(i);
                return WireValue.Encode(new DateTimeOffset(DateTime.SpecifyKind(stamp.Value, DateTimeKind.Unspecified), stamp.GetTimeZoneOffset()));
            }
            return WireValue.Encode(_reader!.GetValue(i));
        }
        catch (Exception e) when (e is OverflowException or InvalidCastException)
        {
            return new WireCell("s", _reader!.GetOracleValue(i).ToString() ?? "");
        }
    }

    private async Task<BridgeResponse> CloseReaderAsync(BridgeRequest request)
    {
        await ReleaseReaderAsync();
        return new BridgeResponse(request.Id, true);
    }

    // Oracle rejects a trailing ';' on a plain statement, but PL/SQL blocks need the one that follows END.
    private static string Normalize(string sql)
    {
        var text = sql.Trim();
        return text.EndsWith(';') && !text.EndsWith("end;", StringComparison.OrdinalIgnoreCase) ? text[..^1].TrimEnd() : text;
    }

    private async Task ReleaseReaderAsync()
    {
        if (_reader is not null)
            await _reader.DisposeAsync();
        if (_command is not null)
            await _command.DisposeAsync();
        _reader = null;
        _command = null;
    }

    /// <summary>Releases the reader and the connection.</summary>
    public async ValueTask DisposeAsync()
    {
        await ReleaseReaderAsync();
        _transaction?.Dispose();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
