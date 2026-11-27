using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>Builds the connect descriptor of an encrypted (TCPS) Oracle connection.</summary>
internal static class OracleDescriptor
{
    /// <summary>Builds the descriptor for a TLS mode.</summary>
    /// <param name="host">Server host.</param>
    /// <param name="port">Listener port.</param>
    /// <param name="service">Service name.</param>
    /// <param name="tls">The profile's TLS mode.</param>
    /// <returns>A <c>(DESCRIPTION=...)</c> string over <c>TCPS</c> for <see cref="TlsMode.Require"/> and <see cref="TlsMode.VerifyFull"/> (the latter also matches the server certificate's name to the host); <see langword="null"/> for the plain <c>host:port/service</c> form.</returns>
    public static string? Build(string host, int port, string service, TlsMode tls)
    {
        if (tls is not (TlsMode.Require or TlsMode.VerifyFull))
            return null;
        var match = tls == TlsMode.VerifyFull ? "yes" : "no";
        return $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCPS)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={service}))(SECURITY=(SSL_SERVER_DN_MATCH={match})))";
    }
}
