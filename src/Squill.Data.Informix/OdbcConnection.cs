namespace Squill.Data.Informix;

/// <summary>One ODBC connection to an Informix server through IBM's CLI driver; every ODBC call runs off the caller's thread.</summary>
internal sealed class OdbcConnection : IDisposable
{
    internal const short HandleEnv = 1, HandleDbc = 2, HandleStmt = 3;
    private const int SqlAttrOdbcVersion = 200, SqlOv3 = 3, SqlAttrAutocommit = 102, SqlAutocommitOff = 0, SqlAutocommitOn = 1;

    private readonly IOdbcApi _api;
    private nint _env;
    private nint _dbc;
    private bool _connected;
    private bool _disposed;

    private OdbcConnection(IOdbcApi api) => _api = api;

    /// <summary>Opens a connection with the real driver.</summary>
    /// <param name="connectionString">A string from <see cref="OdbcConnectionString.Build"/>.</param>
    /// <param name="cancellationToken">Cancels before the call starts; a running connect cannot be interrupted.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="OdbcException">The driver refused the connection (SQLSTATE and message from the diagnostics).</exception>
    /// <exception cref="DllNotFoundException"><c>db2cli64.dll</c> was not found (see <see cref="ClidriverLocator"/>).</exception>
    public static Task<OdbcConnection> OpenAsync(string connectionString, CancellationToken cancellationToken = default) =>
        OpenAsync(connectionString, OdbcApi.Instance, cancellationToken);

    /// <summary>Opens a connection through the given entry points.</summary>
    /// <param name="connectionString">The driver connection string.</param>
    /// <param name="api">The ODBC entry points.</param>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="OdbcException">A step failed; the handles allocated so far are released first.</exception>
    public static Task<OdbcConnection> OpenAsync(string connectionString, IOdbcApi api, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var connection = new OdbcConnection(api);
            try
            {
                Check(api, api.AllocHandle(HandleEnv, 0, out connection._env), HandleEnv, 0);
                Check(api, api.SetEnvAttr(connection._env, SqlAttrOdbcVersion, SqlOv3), HandleEnv, connection._env);
                Check(api, api.AllocHandle(HandleDbc, connection._env, out connection._dbc), HandleEnv, connection._env);
                Check(api, api.DriverConnect(connection._dbc, connectionString), HandleDbc, connection._dbc);
                connection._connected = true;
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }, cancellationToken);

    /// <summary>Gets a value telling whether a manual transaction is open (autocommit is off).</summary>
    public bool InTransaction { get; private set; }

    /// <summary>Executes a statement and positions the reader on its first result set.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <param name="cancellationToken">Cancels before the call starts, or asks the driver to cancel the running statement (which then fails with an <see cref="OdbcException"/>); the connection stays usable.</param>
    /// <returns>The reader; dispose it before running the next statement.</returns>
    /// <exception cref="OdbcException">The statement failed or was cancelled.</exception>
    public Task<OdbcReader> ExecuteAsync(string sql, CancellationToken cancellationToken = default) =>
        ExecuteAsync(sql, [], cancellationToken);

    /// <summary>Executes a statement with positional parameters.</summary>
    /// <param name="sql">The SQL text with one <c>?</c> per value.</param>
    /// <param name="parameters">The values in marker order, see <see cref="OdbcParameter.From"/>; empty runs the text directly.</param>
    /// <param name="cancellationToken">Cancels as for <see cref="ExecuteAsync(string, CancellationToken)"/>.</param>
    /// <returns>The reader; dispose it before running the next statement.</returns>
    /// <exception cref="OdbcException">The statement failed or was cancelled.</exception>
    public Task<OdbcReader> ExecuteAsync(string sql, IReadOnlyList<object?> parameters, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Check(_api, _api.AllocHandle(HandleStmt, _dbc, out var statement), HandleDbc, _dbc);
            var gate = new CancelGate(_api, statement);
            try
            {
                var rc = gate.Run(() => Run(statement, sql, parameters), cancellationToken);
                if (rc != OdbcReader.NoData) // a searched update that touched no row
                    Check(_api, rc, HandleStmt, statement);
                var reader = new OdbcReader(_api, statement, gate);
                reader.LoadResult();
                return reader;
            }
            catch
            {
                _api.FreeHandle(HandleStmt, statement);
                throw;
            }
        }, cancellationToken);

    /// <summary>Turns autocommit off: the next statements belong to a transaction until <see cref="CommitAsync"/> or <see cref="RollbackAsync"/>.</summary>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns>A task that completes when autocommit is off; nothing happens when a transaction is already open.</returns>
    /// <exception cref="OdbcException">The driver refused (for example a database without a transaction log).</exception>
    public Task BeginAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (InTransaction)
                return;
            Check(_api, _api.SetConnectAttr(_dbc, SqlAttrAutocommit, SqlAutocommitOff), HandleDbc, _dbc);
            InTransaction = true;
        }, cancellationToken);

    /// <summary>Commits the open transaction and turns autocommit back on.</summary>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns>A task that completes after the commit; nothing happens without an open transaction.</returns>
    /// <exception cref="OdbcException">The commit failed; the transaction stays open.</exception>
    public Task CommitAsync(CancellationToken cancellationToken = default) => EndAsync(true, cancellationToken);

    /// <summary>Rolls the open transaction back and turns autocommit back on.</summary>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns>A task that completes after the rollback; nothing happens without an open transaction.</returns>
    /// <exception cref="OdbcException">The rollback failed; the transaction stays open.</exception>
    public Task RollbackAsync(CancellationToken cancellationToken = default) => EndAsync(false, cancellationToken);

    private Task EndAsync(bool commit, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!InTransaction)
                return;
            Check(_api, _api.EndTran(_dbc, commit), HandleDbc, _dbc);
            InTransaction = false;
            Check(_api, _api.SetConnectAttr(_dbc, SqlAttrAutocommit, SqlAutocommitOn), HandleDbc, _dbc);
        }, cancellationToken);

    private short Run(nint statement, string sql, IReadOnlyList<object?> parameters)
    {
        if (parameters.Count == 0)
            return _api.ExecDirect(statement, sql);
        Check(_api, _api.Prepare(statement, sql), HandleStmt, statement);
        var bound = new OdbcParameter[parameters.Count]; // kept alive (and pinned) until SQLExecute returns
        for (var i = 0; i < bound.Length; i++)
        {
            bound[i] = OdbcParameter.From(parameters[i]);
            Check(_api, _api.BindParameter(statement, (ushort)(i + 1), bound[i]), HandleStmt, statement);
        }

        var rc = _api.Execute(statement);
        GC.KeepAlive(bound);
        return rc;
    }

    /// <summary>Disconnects and frees the connection and environment handles, in that order.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_connected)
        {
            if (InTransaction)
                _api.EndTran(_dbc, false); // SQLDisconnect fails with an open transaction
            _api.Disconnect(_dbc);
        }

        if (_dbc != 0)
            _api.FreeHandle(HandleDbc, _dbc);
        if (_env != 0)
            _api.FreeHandle(HandleEnv, _env);
    }

    /// <summary>Throws an <see cref="OdbcException"/> unless the call succeeded (<c>SQL_SUCCESS</c> or <c>SQL_SUCCESS_WITH_INFO</c>).</summary>
    /// <param name="api">The ODBC entry points, used to read the diagnostics.</param>
    /// <param name="returnCode">The return code of the call.</param>
    /// <param name="handleType">The type of the handle the call used.</param>
    /// <param name="handle">The handle the call used, 0 when it has none.</param>
    internal static void Check(IOdbcApi api, short returnCode, short handleType, nint handle)
    {
        if (returnCode is 0 or 1)
            return;
        throw OdbcException.From(api, returnCode, handleType, handle);
    }
}
