using System.Text.Json.Serialization;

namespace Squill.Core;

/// <summary>Source-generated JSON metadata for connection profiles.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ConnectionProfile))]
[JsonSerializable(typeof(List<ConnectionProfile>))]
public sealed partial class ProfileJsonContext : JsonSerializerContext;
