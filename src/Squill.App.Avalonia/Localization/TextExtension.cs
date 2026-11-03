using Avalonia.Markup.Xaml;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Localization;

/// <summary>XAML markup extension <c>{loc:Text explorer.title}</c> returning the catalog string of a key; resolved once at load, no reflection.</summary>
/// <param name="key">Catalog key.</param>
public sealed class TextExtension(string key) : MarkupExtension
{
    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(key);
}
