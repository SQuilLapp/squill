using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Services;

/// <summary>The state of one row of the query parameters prompt: the value typed and whether NULL is passed instead.</summary>
/// <param name="name">Parameter name as written in the SQL.</param>
public sealed class ParameterPromptRow(string name)
{
    /// <summary>Gets the parameter name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets or sets the typed value; <see langword="null"/> or empty counts as missing.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets whether NULL is sent instead of the text.</summary>
    public bool IsNull { get; set; }

    /// <summary>Gets the validation message of the row, or <see langword="null"/> when the row can run.</summary>
    public string? Error => IsNull || !string.IsNullOrEmpty(Text) ? null : UiText.Get("editor.valueRequired");

    /// <summary>Tells whether every row can run.</summary>
    /// <param name="rows">The rows of the prompt.</param>
    /// <returns><see langword="true"/> when no row has an error.</returns>
    public static bool AllValid(IEnumerable<ParameterPromptRow> rows) => rows.All(r => r.Error is null);
}
