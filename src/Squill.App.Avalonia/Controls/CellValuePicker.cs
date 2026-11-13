using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Squill.Presentation.Editing;

namespace Squill.App.Avalonia.Controls;

/// <summary>The popup that helps to fill a boolean, a date or a date with a time; the text box stays editable by hand.</summary>
internal static class CellValuePicker
{
    /// <summary>Opens the popup under the text box.</summary>
    /// <param name="anchor">The text box being edited.</param>
    /// <param name="kind">Boolean, date or date with a time.</param>
    /// <param name="current">The text in the box, where the popup starts from.</param>
    /// <param name="canDefault">Whether to offer the choice that leaves the cell to the default value of its column.</param>
    /// <param name="picked">Receives the text each choice produces, or <see langword="null"/> for the default value.</param>
    public static void Show(Control anchor, CellEditorKind kind, string current, bool canDefault, Action<string?> picked)
    {
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        flyout.Content = kind == CellEditorKind.Boolean ? Choices(flyout, current, canDefault, picked) : Moment(flyout, kind, current, canDefault, picked);
        flyout.ShowAt(anchor);
    }

    // A compact list, like a menu: the current value is highlighted, a click or Enter picks.
    private static ListBox Choices(Flyout flyout, string current, bool canDefault, Action<string?> picked)
    {
        var choices = CellEditorKinds.BooleanChoices.Select(c => (Text: (string?)c.Text, c.Label)).ToList();
        if (canDefault)
            choices.Add((null, CellEditorKinds.DefaultLabel));
        var list = new ListBox { ItemsSource = choices.Select(c => c.Label).ToList(), MinWidth = 120 };
        list.SelectedIndex = choices.FindIndex(c => string.Equals(c.Text, current.Trim(), StringComparison.OrdinalIgnoreCase));

        void Pick()
        {
            if (list.SelectedIndex < 0)
                return;
            picked(choices[list.SelectedIndex].Text);
            flyout.Hide();
        }

        list.Tapped += (_, _) => Pick();
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            Pick();
            e.Handled = true;
        };
        flyout.Opened += (_, _) => list.Focus();
        return list;
    }

    private static StackPanel Moment(Flyout flyout, CellEditorKind kind, string current, bool canDefault, Action<string?> picked)
    {
        var start = CellEditorKinds.ParseMoment(current);
        var day = Combo(Range(1, 31, "0"), start.Day.ToString(CultureInfo.InvariantCulture), 70);
        var month = new ComboBox { ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToArray(), SelectedIndex = start.Month - 1, MinWidth = 130 };
        var years = Math.Max(1, start.Year - 100);
        var year = Combo([.. Enumerable.Range(years, Math.Min(9999, start.Year + 100) - years + 1).Select(y => y.ToString(CultureInfo.InvariantCulture))], start.Year.ToString(CultureInfo.InvariantCulture), 90);
        var hour = Combo(Range(0, 23, "00"), start.Hour.ToString("00", CultureInfo.InvariantCulture), 70);
        var minute = Combo(Range(0, 59, "00"), start.Minute.ToString("00", CultureInfo.InvariantCulture), 70);
        var second = Combo(Range(0, 59, "00"), start.Second.ToString("00", CultureInfo.InvariantCulture), 70);

        int Of(ComboBox box, int fallback) => box.SelectedItem is string s && int.TryParse(s, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        void Report() => picked(CellEditorKinds.FormatMoment(
            kind, Of(year, start.Year), month.SelectedIndex + 1, Of(day, start.Day), Of(hour, 0), Of(minute, 0), Of(second, 0)));

        foreach (var box in new[] { day, month, year, hour, minute, second })
            box.SelectionChanged += (_, _) => Report();

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
