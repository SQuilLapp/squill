namespace Squill.Presentation.Palette;

/// <summary>A command the palette can run; texts are <c>UiText</c> keys resolved at search time so a language change needs no rebuild.</summary>
/// <param name="Id">The stable identifier, such as <c>query.run</c>; unique in a registry.</param>
/// <param name="TitleKey">The catalog key of the title.</param>
/// <param name="CategoryKey">The catalog key of the category (a <c>menu.*</c> key).</param>
/// <param name="Shortcut">The shortcut to display, only when every head binds it; otherwise <see langword="null"/>.</param>
/// <param name="IconKey">A logical icon key.</param>
/// <param name="CanRun">Whether the action can run now.</param>
/// <param name="Run">Runs the action.</param>
/// <param name="TitleKeySource">When set, gives the title key at search time instead of <paramref name="TitleKey"/> (a title that follows a state).</param>
public sealed record PaletteAction(
    string Id,
    string TitleKey,
    string CategoryKey,
    string? Shortcut,
    string IconKey,
    Func<bool> CanRun,
    Func<CancellationToken, ValueTask> Run,
    Func<string>? TitleKeySource = null);
