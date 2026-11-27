namespace Squill.Data.Informix;

/// <summary>Builds the DSN-less connection string of IBM's CLI driver.</summary>
internal static class OdbcConnectionString
{
    /// <summary>Builds the string; values containing <c>;</c> or <c>}</c> (or starting with <c>{</c>) are braced with <c>}</c> doubled.</summary>
    /// <param name="host">The server host.</param>
    /// <param name="port">The DRDA port (9089 on the developer image).</param>
    /// <param name="database">The database name.</param>
    /// <param name="user">The login.</param>
    /// <param name="password">The password; it is only ever inside the returned string.</param>
    /// <returns>The connection string.</returns>
    public static string Build(string host, int port, string database, string user, string password) =>
        $"DATABASE={Escape(database)};HOSTNAME={Escape(host)};PORT={port};PROTOCOL=TCPIP;UID={Escape(user)};PWD={Escape(password)}";

    private static string Escape(string value) =>
        value.AsSpan().IndexOfAny(';', '}') >= 0 || value.StartsWith('{') ? "{" + value.Replace("}", "}}", StringComparison.Ordinal) + "}" : value;
}
