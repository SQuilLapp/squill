using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Squill.Presentation.Editing;

namespace Squill.App.Avalonia.Controls;

/// <summary>The popup that helps to fill a boolean, a date or a date with a time; the text box stays editable by hand.</summary>
internal static class CellValuePicker
{
    /// <summary>Opens the popup under the text box.</summary>
    /// <param name="anchor">The text box being edited.</param>
    /// <param name="kind">Boolean, date or date with a time.</param>
    /// <param name="current">The text in the box, where a date popup starts from.</param>
    /// <param name="picked">Receives the text each choice produces.</param>
    public static void Show(Control anchor, CellEditorKind kind, string current, Action<string> picked)
    {
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        flyout.Content = kind == CellEditorKind.Boolean ? Choices(flyout, picked) : Moment(kind, current, picked);
        flyout.ShowAt(anchor);
    }

    private static StackPanel Choices(Flyout flyout, Action<string> picked)
    {
        var panel = new StackPanel { Spacing = 4, MinWidth = 120 };
        foreach (var (text, label) in CellEditorKinds.BooleanChoices)
        {
            var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                picked(text);
                flyout.Hide();
            };
            panel.Children.Add(button);
        }

        return panel;
    }

    private static StackPanel Moment(CellEditorKind kind, string current, Action<string> picked)
    {
        var start = CellEditorKinds.ParseMoment(current);
        var day = new ComboBox { ItemsSource = Enumerable.Range(1, 31).ToArray(), SelectedItem = start.Day, MinWidth = 70 };
        var month = new ComboBox { ItemsSource = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12).ToArray(), SelectedIndex = start.Month - 1, MinWidth = 130 };
        var year = Spinner(start.Year, 1, 9999, 90);
        var hour = Spinner(start.Hour, 0, 23, 70);
        var minute = Spinner(start.Minute, 0, 59, 70);
        var second = Spinner(start.Second, 0, 59, 70);

        void Report() => picked(CellEditorKinds.FormatMoment(
            kind, (int)(year.Value ?? start.Year), month.SelectedIndex + 1, day.SelectedItem is int d ? d : start.Day,
            (int)(hour.Value ?? 0), (int)(minute.Value ?? 0), (int)(second.Value ?? 0)));

        day.SelectionChanged += (_, _) => Report();
        month.SelectionChanged += (_, _) => Report();
        year.ValueChanged += (_, _) => Report();
        hour.ValueChanged += (_, _) => Report();
        minute.ValueChanged += (_, _) => Report();
        second.ValueChanged += (_, _) => Report();

        var panel = new StackPanel { Spacing = 8 };
        var date = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        date.Children.Add(day);
        date.Children.Add(month);
        date.Children.Add(year);
        panel.Children.Add(date);
        if (kind == CellEditorKind.DateTime)
        {
            var time = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
            time.Children.Add(hour);
            time.Children.Add(new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center });
            time.Children.Add(minute);
            time.Children.Add(new TextBlock { Text = ":", VerticalAlignment = VerticalAlignment.Center });
            time.Children.Add(second);
            panel.Children.Add(time);
        }

        return panel;
    }

    private static NumericUpDown Spinner(int value, int min, int max, double width) =>
        new() { Minimum = min, Maximum = max, Value = value, Increment = 1, FormatString = "0", Width = width };
}
