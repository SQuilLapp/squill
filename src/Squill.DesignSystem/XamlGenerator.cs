using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Squill.DesignSystem;

/// <summary>Generates the WinUI 3 and Avalonia resource dictionaries from squill.tokens.json.</summary>
public static class XamlGenerator
{
    /// <summary>Parses the token file.</summary>
    /// <param name="json">Content of squill.tokens.json.</param>
    /// <returns>The parsed tokens.</returns>
    /// <exception cref="JsonException">The text is not valid JSON.</exception>
    /// <exception cref="KeyNotFoundException">A required token group is missing.</exception>
    // JsonDocument instead of reflection-based binding: stays AOT-safe without a source-generated context.
    public static TokenSet Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("squill");
        var themes = new Dictionary<string, Dictionary<string, string>> { ["dark"] = [], ["light"] = [] };
        foreach (var c in root.GetProperty("color").EnumerateObject())
            foreach (var theme in themes.Keys)
                themes[theme][c.Name] = c.Value.GetProperty("$value").GetProperty(theme).GetString()!;

        var density = root.GetProperty("density");
        return new TokenSet(themes, new Metrics(
            RowHeight: density.EnumerateObject().ToDictionary(d => d.Name, d => Px(d.Value.GetProperty("row"))),
            ControlHeight: density.EnumerateObject().ToDictionary(d => d.Name, d => Px(d.Value.GetProperty("control"))),
            Radius: root.GetProperty("radius").EnumerateObject().ToDictionary(r => r.Name, r => Px(r.Value)),
            MotionMs: root.GetProperty("motion").EnumerateObject().ToDictionary(m => m.Name, m => Px(m.Value))));
    }

    // "26px" / "150ms" -> 26 / 150
    private static int Px(JsonElement token) =>
        int.Parse(new string(token.GetProperty("$value").GetString()!.TakeWhile(char.IsDigit).ToArray()), CultureInfo.InvariantCulture);

    /// <summary>Converts a kebab-case name to PascalCase.</summary>
    /// <param name="kebab">Name whose parts are separated by hyphens, such as <c>accent-hover</c>.</param>
    /// <returns>The PascalCase name, such as <c>AccentHover</c>.</returns>
    public static string Pascal(string kebab) =>
        string.Concat(kebab.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    /// <summary>Converts a CSS color to the XAML "#AARRGGBB" form.</summary>
    /// <param name="css">A "#RRGGBB" or "rgba(r,g,b,a)" color.</param>
    /// <returns>The color as "#AARRGGBB".</returns>
    public static string Argb(string css)
    {
        css = css.Trim();
        if (css.StartsWith('#')) return css.Length == 7 ? "#FF" + css[1..].ToUpperInvariant() : css.ToUpperInvariant();
        var p = css[(css.IndexOf('(') + 1)..^1].Split(',');
        var a = (int)Math.Round(double.Parse(p[3], CultureInfo.InvariantCulture) * 255);
        return $"#{a:X2}{int.Parse(p[0], CultureInfo.InvariantCulture):X2}{int.Parse(p[1], CultureInfo.InvariantCulture):X2}{int.Parse(p[2], CultureInfo.InvariantCulture):X2}";
    }

    /// <summary>Generates the WinUI 3 resource dictionary.</summary>
    /// <param name="t">Tokens to emit.</param>
    /// <returns>The XAML text.</returns>
    // shortcut: no WinUI HighContrast theme, add it with the views (SystemColor* resources).
    public static string WinUi(TokenSet t)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Generated from tokens/squill.tokens.json - do not edit -->");
        sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("  <ResourceDictionary.ThemeDictionaries>");
        foreach (var (theme, key) in new[] { ("dark", "Default"), ("light", "Light") })
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    <ResourceDictionary x:Key=\"{key}\">");
            foreach (var (name, v) in t.Themes[theme].Where(kv => IsColor(kv.Value)))
                sb.AppendLine(CultureInfo.InvariantCulture, $"      <Color x:Key=\"Squill{Pascal(name)}Color\">{Argb(v)}</Color>");
            foreach (var name in t.Themes[theme].Where(kv => IsColor(kv.Value)).Select(kv => kv.Key))
                sb.AppendLine(CultureInfo.InvariantCulture, $"      <SolidColorBrush x:Key=\"Squill{Pascal(name)}Brush\" Color=\"{{ThemeResource Squill{Pascal(name)}Color}}\"/>");
            sb.AppendLine("    </ResourceDictionary>");
        }
        sb.AppendLine("  </ResourceDictionary.ThemeDictionaries>");
        AppendMetrics(sb, t.Metrics);
        sb.AppendLine("</ResourceDictionary>");
        return sb.ToString();
    }

    /// <summary>Generates the Avalonia resource dictionary.</summary>
    /// <param name="t">Tokens to emit.</param>
    /// <returns>The XAML text.</returns>
    public static string Avalonia(TokenSet t)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Generated from tokens/squill.tokens.json - do not edit -->");
        sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("  <ResourceDictionary.ThemeDictionaries>");
        foreach (var (theme, key) in new[] { ("dark", "Dark"), ("light", "Light") })
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    <ResourceDictionary x:Key=\"{key}\">");
            foreach (var (name, v) in t.Themes[theme].Where(kv => IsColor(kv.Value)))
                sb.AppendLine(CultureInfo.InvariantCulture, $"      <SolidColorBrush x:Key=\"Squill{Pascal(name)}Brush\" Color=\"{Argb(v)}\"/>");
            sb.AppendLine("    </ResourceDictionary>");
        }
        sb.AppendLine("  </ResourceDictionary.ThemeDictionaries>");
        AppendMetrics(sb, t.Metrics);
        sb.AppendLine("</ResourceDictionary>");
        return sb.ToString();
    }

    // Shadows (--shadow-*) are not colors: handled with the views.
    private static bool IsColor(string v) => v.StartsWith('#') || v.StartsWith("rgba(", StringComparison.Ordinal);

    private static void AppendMetrics(StringBuilder sb, Metrics m)
    {
        foreach (var (k, v) in m.RowHeight) sb.AppendLine(CultureInfo.InvariantCulture, $"  <x:Double x:Key=\"SquillRowHeight{Pascal(k)}\">{v}</x:Double>");
        foreach (var (k, v) in m.ControlHeight) sb.AppendLine(CultureInfo.InvariantCulture, $"  <x:Double x:Key=\"SquillControlHeight{Pascal(k)}\">{v}</x:Double>");
        foreach (var (k, v) in m.Radius) sb.AppendLine(CultureInfo.InvariantCulture, $"  <CornerRadius x:Key=\"SquillRadius{Pascal(k)}\">{v}</CornerRadius>");
    }
}
