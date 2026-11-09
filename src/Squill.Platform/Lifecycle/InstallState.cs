using System.Text.Json.Serialization;

namespace Squill.Platform.Lifecycle;

/// <summary>What survives between runs about the installation: only the version string, never a secret.</summary>
/// <param name="LastRunVersion">SemVer 2 version of the build that ran last.</param>
public sealed record InstallState(string LastRunVersion);

/// <summary>Source-generated JSON metadata for <see cref="InstallState"/>.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InstallState))]
public sealed partial class InstallStateJsonContext : JsonSerializerContext;
