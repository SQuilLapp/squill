using System.Globalization;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.Presentation.Plans;

/// <summary>Formats the figures and warnings of a plan in the current language; a figure the engine did not give is shown as an en dash, never invented.</summary>
internal static class PlanText
{
    /// <summary>The text of a missing figure.</summary>
    public const string Missing = "–";

    /// <summary>Formats a ratio in [0, 1] as a whole percentage.</summary>
    /// <param name="share">The ratio, or null.</param>
    /// <returns>For example <c>64 %</c> for 0.64 and <c>0 %</c> for 0.004.</returns>
    public static string Percent(double? share) =>
        share is { } s ? string.Create(UiText.Culture, $"{Math.Round(s * 100, MidpointRounding.AwayFromZero):0} %") : Missing;

    /// <summary>Formats a count of rows or executions.</summary>
    /// <param name="value">The count, or null.</param>
    /// <returns>The count with the group separator of the language.</returns>
    public static string Count(double? value) => value is { } v ? DisplayFormat.Count((long)Math.Round(v, MidpointRounding.AwayFromZero)) : Missing;

    /// <summary>Formats an engine cost, which has no unit.</summary>
    /// <param name="cost">The cost, or null.</param>
    /// <returns>The cost with at most two decimals.</returns>
    public static string Cost(double? cost) => cost is { } c ? c.ToString("#,##0.##", UiText.Culture) : Missing;

    /// <summary>Formats a duration given in milliseconds.</summary>
    /// <param name="milliseconds">The duration, or null.</param>
    /// <returns>For example <c>0,4 ms</c> below one millisecond, <c>1 182 ms</c> above.</returns>
    public static string Milliseconds(double? milliseconds) => milliseconds switch
    {
        null => Missing,
        < 1 => string.Create(UiText.Culture, $"{milliseconds:0.0#} ms"),
        _ => string.Create(UiText.Culture, $"{milliseconds:#,##0} ms"),
    };

    /// <summary>Formats a size in bytes.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>For example <c>431 Mo</c> in French and <c>431 MB</c> in English.</returns>
    public static string Size(long bytes)
    {
        var (key, value) = bytes switch
        {
            >= 1L << 30 => ("plan.size.gb", bytes / (double)(1L << 30)),
            >= 1L << 20 => ("plan.size.mb", bytes / (double)(1L << 20)),
            >= 1L << 10 => ("plan.size.kb", bytes / (double)(1L << 10)),
            _ => ("plan.size.b", (double)bytes),
        };
        return UiText.Format(key, value.ToString(value < 10 && bytes >= 1L << 10 ? "0.#" : "0", UiText.Culture));
    }

    /// <summary>Renders a warning in the current language.</summary>
    /// <param name="warning">The warning with its arguments.</param>
    /// <returns>The text; an unknown code is shown as is so that nothing is hidden.</returns>
    public static string Warning(PlanWarning warning)
    {
        var args = warning.Args;
        string Arg(int i) => i < args.Count ? args[i] : "";
        return warning.Code switch
        {
            PlanMessageCodes.Misestimate => UiText.Format("plan.warning.misestimate", Arg(0), UiText.Get(Arg(1) == "under" ? "plan.warning.misestimate.low" : "plan.warning.misestimate.high")),
            PlanMessageCodes.FullScan => UiText.Get("plan.warning.fullscan"),
            PlanMessageCodes.SortSpill => Arg(1).Length > 0 ? UiText.Format("plan.warning.sortspill.size", Arg(1)) : UiText.Get("plan.warning.sortspill"),
            PlanMessageCodes.HashSpill => UiText.Get("plan.warning.hashspill"),
            PlanMessageCodes.MissingIndex => UiText.Format("plan.warning.missingindex", double.TryParse(Arg(0), NumberStyles.Float, CultureInfo.InvariantCulture, out var impact) ? impact.ToString("0.#", UiText.Culture) : Arg(0)),
            PlanMessageCodes.ImplicitConversion => UiText.Format("plan.warning.implicitconversion", Arg(0)),
            PlanMessageCodes.NoIndex => UiText.Format("plan.warning.noindex", Arg(0)),
            PlanMessageCodes.NoStats => UiText.Format("plan.warning.nostats", Arg(0)),
            PlanMessageCodes.NoJoinPredicate => UiText.Get("plan.warning.nojoinpredicate"),
            _ => warning.Code,
        };
    }

    /// <summary>Translates the name of a property the engines print in English and the UI knows.</summary>
    /// <param name="name">The name as the engine prints it.</param>
    /// <returns>The translated name, or <paramref name="name"/> when it is not a known one.</returns>
    public static string PropertyName(string name) => name switch
    {
        "Filter" => UiText.Get("plan.prop.filter"),
        "Rows Removed by Filter" => UiText.Get("plan.prop.rowsRemoved"),
        "Buffers" => UiText.Get("plan.prop.buffers"),
        "Workers" or "Workers Planned" or "Workers Launched" => UiText.Get("plan.prop.workers"),
        _ => name,
    };

    /// <summary>Tells whether <see cref="PropertyName"/> translates a name.</summary>
    /// <param name="name">The name as the engine prints it.</param>
    /// <returns><see langword="true"/> for the few known names.</returns>
    public static bool IsKnownProperty(string name) => name is "Filter" or "Rows Removed by Filter" or "Buffers" or "Workers" or "Workers Planned" or "Workers Launched";

    /// <summary>Formats a ratio of estimate to actual as a multiplier.</summary>
    /// <param name="factor">The factor, or null.</param>
    /// <returns>For example <c>x590</c>, or an empty text without a factor.</returns>
    public static string Factor(double? factor) => factor is { } f ? "x" + f.ToString("0.#", UiText.Culture) : "";
}
