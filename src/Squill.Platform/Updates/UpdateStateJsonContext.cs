using System.Text.Json.Serialization;

namespace Squill.Platform.Updates;

/// <summary>Source-generated JSON metadata for the update state file.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<UpdateState>))]
public sealed partial class UpdateStateJsonContext : JsonSerializerContext;
