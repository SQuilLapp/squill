using System.Text.Json.Serialization;

namespace Squill.Platform.Storage;

/// <summary>Source-generated JSON metadata for the history, snippet, diagram layout and table filter files.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<HistoryEntry>))]
[JsonSerializable(typeof(List<Snippet>))]
[JsonSerializable(typeof(DiagramLayout))]
[JsonSerializable(typeof(TableFilter))]
public sealed partial class StorageJsonContext : JsonSerializerContext;
