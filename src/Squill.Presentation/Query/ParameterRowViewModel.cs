using CommunityToolkit.Mvvm.ComponentModel;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Query;

/// <summary>One row of the query parameters panel: the value typed for a named parameter, or NULL.</summary>
public sealed partial class ParameterRowViewModel : ObservableObject
{
    /// <summary>Creates a row.</summary>
    /// <param name="name">Parameter name without its sigil.</param>
    /// <param name="text">Initial text, usually the value of the previous run.</param>
    /// <param name="isNull">Whether NULL is passed initially.</param>
    public ParameterRowViewModel(string name, string? text, bool isNull)
    {
        Name = name;
        Text = text;
        IsNull = isNull;
    }

    /// <summary>Gets the parameter name without its sigil.</summary>
    public string Name { get; }

    /// <summary>Gets or sets the typed value; <see langword="null"/> or empty counts as missing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError))]
    public partial string? Text { get; set; }

    /// <summary>Gets or sets whether NULL is sent instead of the text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(HasError), nameof(IsValueEnabled))]
    public partial bool IsNull { get; set; }

    /// <summary>Gets whether the value field accepts input (it does not while NULL is passed).</summary>
    public bool IsValueEnabled => !IsNull;

    /// <summary>Gets the validation message, or <see langword="null"/> when the row can run.</summary>
    public string? Error => IsNull || !string.IsNullOrEmpty(Text) ? null : UiText.Get("editor.valueRequired");

    /// <summary>Gets whether <see cref="Error"/> is set.</summary>
    public bool HasError => Error is not null;

    internal ParameterValue ToValue() => new(Name, Text, IsNull);
}
