using System.Text.Json.Serialization;

namespace Squill.Platform.Storage;

/// <summary>Source-generated JSON metadata for the history and snippet files.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<HistoryEntry>))]
[JsonSerializable(typeof(List<Snippet>))]
public sealed partial class StorageJsonContext : JsonSerializerContext;
