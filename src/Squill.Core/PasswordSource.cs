namespace Squill.Core;

/// <summary>Where the database password of a profile comes from.</summary>
public enum PasswordSource
{
    /// <summary>Read from the operating system secret store.</summary>
    Stored,
    /// <summary>Asked at each connect and kept in memory for that session only.</summary>
    Ask,
    /// <summary>Printed on standard output by <see cref="ConnectionProfile.PasswordCommand"/>.</summary>
    Command,
}
