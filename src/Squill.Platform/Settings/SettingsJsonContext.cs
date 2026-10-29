using System.Text.Json.Serialization;

namespace Squill.Platform.Settings;

/// <summary>Source-generated JSON metadata for the settings file.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<AppSettings>))]
public sealed partial class SettingsJsonContext : JsonSerializerContext;
