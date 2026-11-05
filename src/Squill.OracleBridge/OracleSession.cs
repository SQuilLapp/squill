using Oracle.ManagedDataAccess.Client;
using Squill.Data.Oracle;

namespace Squill.OracleBridge;

/// <summary>The state of one bridged connection: the open Oracle connection and at most one open reader.</summary>
internal sealed class OracleSession : IAsyncDisposable
{
    private OracleConnection? _connection;
    private OracleDataReader? _reader;
    private OracleCommand? _command;
    private int _commandTimeout;

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
            DataSource = $"{request.Host}:{request.Port}/{request.Service}",
            UserID = request.User,
            Password = request.Password,
            ConnectionTimeout = request.ConnectTimeoutSeconds ?? 15,
        };
        _commandTimeout = request.CommandTimeoutSeconds ?? 0;
        _connection = new OracleConnection(builder.ConnectionString);
        await _connection.OpenAsync();
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
        _command.CommandTimeout = _commandTimeout;
        foreach (var p in request.Parameters ?? [])
        {
            var value = WireValue.Decode(p.Value);
            // ODP.NET has no BOOLEAN bind before 23ai: send 1/0.
            _command.Parameters.Add(new OracleParameter(p.Name.TrimStart(':', '@'), value is bool b ? (b ? 1 : 0) : value ?? DBNull.Value));
        }

        _reader = (OracleDataReader)await _command.ExecuteReaderAsync();
        var columns = Enumerable.Range(0, _reader.FieldCount)
            .Select(i => new WireColumn(_reader.GetName(i), _reader.GetDataTypeName(i), WireValue.KindOf(_reader.GetFieldType(i))))
            .ToArray();
        if (columns.Length > 0)
            return new BridgeResponse(request.Id, true, Columns: columns);

        var affected = _reader.RecordsAffected;
        await ReleaseReaderAsync();
        return new BridgeResponse(request.Id, true, Columns: columns, Done: true, RecordsAffected: affected);
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
        if (done)
            await ReleaseReaderAsync();
        return new BridgeResponse(request.Id, true, Rows: [.. rows], Done: done, RecordsAffected: affected);
    }

    // NUMBER values beyond decimal's range, and exotic types, fall back to their text form.
    private WireCell? ReadCell(int i)
    {
        try
        {
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
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
