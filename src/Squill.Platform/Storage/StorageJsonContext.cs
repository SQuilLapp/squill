using System.Text.Json.Serialization;

namespace Squill.Platform.Storage;

/// <summary>Source-generated JSON metadata for the history, snippet, diagram layout and table filter and known hosts files.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<HistoryEntry>))]
[JsonSerializable(typeof(List<Snippet>))]
[JsonSerializable(typeof(List<Squill.Platform.Profiles.RecentConnection>))]
[JsonSerializable(typeof(DiagramLayout))]
[JsonSerializable(typeof(TableFilter))]
[JsonSerializable(typeof(List<Squill.Platform.Ssh.KnownHostEntry>))]
public sealed partial class StorageJsonContext : JsonSerializerContext;
