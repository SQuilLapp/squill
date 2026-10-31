using System.Globalization;
using Avalonia.Data.Converters;
using Squill.Core;
using Squill.Presentation.Sessions;

namespace Squill.App.Avalonia.Views;

/// <summary>Tells whether a profile has an open session; the second value (the session count) only triggers re-evaluation.</summary>
internal sealed class ConnectedProfileConverter : IMultiValueConverter
{
    /// <summary>Shared instance for XAML.</summary>
    public static readonly ConnectedProfileConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [ConnectionProfile profile, IEnumerable<ConnectionSession> sessions, ..] && sessions.Any(s => s.Profile.Id == profile.Id);
}
