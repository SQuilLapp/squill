using System.Globalization;
using Avalonia.Data.Converters;
using Squill.Core.Design;

namespace Squill.App.Avalonia.Views;

/// <summary>Tells whether an error issue names a row; the first value is the row name, the second the issue list.</summary>
internal sealed class DesignIssueSubjectConverter : IMultiValueConverter
{
    /// <summary>Shared instance for XAML.</summary>
    public static readonly DesignIssueSubjectConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [string name, IEnumerable<DesignIssue> issues, ..]
        && issues.Any(i => i.Severity == DesignIssueSeverity.Error && string.Equals(i.Subject, name, StringComparison.OrdinalIgnoreCase));
}
