using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Squill.Presentation.Editing;

namespace Squill.App.WinUI.Controls;

/// <summary>The popup that helps to fill a boolean, a date or a date with a time; the text box stays editable by hand.</summary>
internal static class CellValuePicker
{
    /// <summary>Opens the popup under the text box.</summary>
    /// <param name="anchor">The text box being edited.</param>
    /// <param name="kind">Boolean, date or date with a time.</param>
    /// <param name="current">The text in the box, where the popup starts from.</param>
    /// <param name="canDefault">Whether to offer the choice that leaves the cell to the default value of its column.</param>
    /// <param name="picked">Receives the text each choice produces, or <see langword="null"/> for the default value.</param>
    public static void Show(FrameworkElement anchor, CellEditorKind kind, string current, bool canDefault, Action<string?> picked)
    {
        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        flyout.Content = kind == CellEditorKind.Boolean ? Choices(flyout, current, canDefault, picked) : Moment(flyout, kind, current, canDefault, picked);
        flyout.ShowAt(anchor);
    }

    // A compact list, like a menu: the current value is highlighted, a click picks.
    private static ListView Choices(Flyout flyout, string current, bool canDefault, Action<string?> picked)
    {
        var choices = CellEditorKinds.BooleanChoices.Select(c => (Text: (string?)c.Text, c.Label)).ToList();
        if (canDefault)
            choices.Add((null, CellEditorKinds.DefaultLabel));
        var list = new ListView { ItemsSource = choices.Select(c => c.Label).ToList(), MinWidth = 140, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
        list.SelectedIndex = choices.FindIndex(c => string.Equals(c.Text, current.Trim(), StringComparison.OrdinalIgnoreCase));
        list.ItemClick += (_, e) =>
        {
            picked(choices.First(c => c.Label == (string)e.ClickedItem).Text);
            flyout.Hide();
        };
        return list;
    }

    private static StackPanel Moment(Flyout flyout, CellEditorKind kind, string current, bool canDefault, Action<string?> picked)
    {
        var start = CellEditorKinds.ParseMoment(current);
        var day = Combo(Range(1, 31, "0"), start.Day.ToString(CultureInfo.InvariantCulture), 80);
        var month = new ComboBox { ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToArray(), SelectedIndex = start.Month - 1, MinWidth = 140 };
        var years = Math.Max(1, start.Year - 100);
        var year = Combo([.. Enumerable.Range(years, Math.Min(9999, start.Year + 100) - years + 1).Select(y => y.ToString(CultureInfo.InvariantCulture))], start.Year.ToString(CultureInfo.InvariantCulture), 100);
        var hour = Combo(Range(0, 23, "00"), start.Hour.ToString("00", CultureInfo.InvariantCulture), 80);
        var minute = Combo(Range(0, 59, "00"), start.Minute.ToString("00", CultureInfo.InvariantCulture), 80);
        var second = Combo(Range(0, 59, "00"), start.Second.ToString("00", CultureInfo.InvariantCulture), 80);

        int Of(ComboBox box, int fallback) => box.SelectedItem is string s && int.TryParse(s, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        void Report() => picked(CellEditorKinds.FormatMoment(
            kind, Of(year, start.Year), month.SelectedIndex + 1, Of(day, start.Day), Of(hour, 0), Of(minute, 0), Of(second, 0)));

        // The day list follows the month and the year, so 31 February cannot be picked.
        var syncing = true;

        void FitDays()
        {
            var length = DateTime.DaysInMonth(Math.Clamp(Of(year, start.Year), 1, 9999), Math.Clamp(month.SelectedIndex + 1, 1, 12));
            var chosen = Math.Min(Of(day, start.Day), length);
            day.ItemsSource = Range(1, length, "0");
            day.SelectedItem = chosen.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var box in new[] { day, month, year, hour, minute, second })
        {
            box.SelectionChanged += (_, _) =>
            {
                if (syncing)
                    return;
                syncing = true;
                if (box == month || box == year)
                    FitDays();
                syncing = false;
                Report();
            };
        }

        FitDays();
        syncing = false;

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Row(day, month, year));
        if (kind == CellEditorKind.DateTime)
            panel.Children.Add(Row(hour, minute, second));
        if (canDefault)
        {
            var useDefault = new Button { Content = CellEditorKinds.DefaultLabel, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            useDefault.Click += (_, _) =>
            {
                picked(null);
                flyout.Hide();
            };
            panel.Children.Add(useDefault);
        }

        return panel;
    }

    private static string[] Range(int from, int to, string format) =>
        [.. Enumerable.Range(from, to - from + 1).Select(i => i.ToString(format, CultureInfo.InvariantCulture))];

    private static ComboBox Combo(string[] items, string selected, double width) =>
        new() { ItemsSource = items, SelectedItem = selected, MinWidth = width };

    private static StackPanel Row(params ComboBox[] boxes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var box in boxes)
            row.Children.Add(box);
        return row;
    }
}
