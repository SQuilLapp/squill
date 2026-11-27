namespace Squill.Core;

/// <summary>Protocol spoken to a connection proxy.</summary>
public enum ProxyKind
{
    /// <summary>SOCKS version 5 (RFC 1928), with optional user and password (RFC 1929).</summary>
    Socks5,
    /// <summary>HTTP <c>CONNECT</c> tunnel, with optional Basic authentication.</summary>
    HttpConnect,
}

/// <summary>Proxy a connection goes through; the password lives in the secret store.</summary>
/// <param name="Kind">Protocol of the proxy.</param>
/// <param name="Host">Proxy host name or address.</param>
/// <param name="Port">Proxy port.</param>
/// <param name="UserName">Account for the proxy; <see langword="null"/> or empty for an open proxy.</param>
public sealed record ProxySettings(ProxyKind Kind, string Host, int Port, string? UserName = null);
