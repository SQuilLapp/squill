namespace Squill.Data.Informix;

/// <summary>One ODBC connection to an Informix server through IBM's CLI driver; every ODBC call runs off the caller's thread.</summary>
internal sealed class OdbcConnection : IDisposable
{
    internal const short HandleEnv = 1, HandleDbc = 2, HandleStmt = 3;
    private const int SqlAttrOdbcVersion = 200, SqlOv3 = 3;

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

    /// <summary>Executes a statement and positions the reader on its first result set.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <param name="cancellationToken">Cancels before the call starts.</param>
    /// <returns>The reader; dispose it before running the next statement.</returns>
    /// <exception cref="OdbcException">The statement failed.</exception>
    public Task<OdbcTextReader> ExecuteAsync(string sql, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Check(_api, _api.AllocHandle(HandleStmt, _dbc, out var statement), HandleDbc, _dbc);
            try
            {
                var rc = _api.ExecDirect(statement, sql);
                if (rc != OdbcTextReader.NoData) // a searched update that touched no row
                    Check(_api, rc, HandleStmt, statement);
                var reader = new OdbcTextReader(_api, statement);
                reader.LoadResult();
                return reader;
            }
            catch
            {
                _api.FreeHandle(HandleStmt, statement);
                throw;
            }
        }, cancellationToken);

    /// <summary>Disconnects and frees the connection and environment handles, in that order.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_connected)
            _api.Disconnect(_dbc);
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
