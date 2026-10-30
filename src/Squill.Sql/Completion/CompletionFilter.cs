namespace Squill.Sql.Completion;

/// <summary>Client-side filtering of a completion list.</summary>
public static class CompletionFilter
{
    /// <summary>Keeps the items matching <paramref name="prefix"/>, case-insensitively: prefix matches first, then the others that contain it.</summary>
    /// <param name="items">The candidates.</param>
    /// <param name="prefix">The text typed so far; empty keeps everything.</param>
    /// <returns>The matching items, each group keeping the input order.</returns>
    public static IReadOnlyList<CompletionItem> Filter(IReadOnlyList<CompletionItem> items, string prefix)
    {
        if (prefix.Length == 0)
        {
            return items;
        }

        var starts = new List<CompletionItem>();
        var contains = new List<CompletionItem>();
        foreach (var item in items)
        {
            var index = item.Label.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index == 0)
            {
                starts.Add(item);
            }
            else if (index > 0)
            {
                contains.Add(item);
            }
        }

        starts.AddRange(contains);
        return starts;
    }
}
