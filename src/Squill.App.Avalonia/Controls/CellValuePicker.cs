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
        flyout.Content = kind == CellEditorKind.Boolean
            ? Choices(flyout, new ChoicePicker(current, canDefault), picked)
            : Moment(flyout, new MomentPicker(kind, current), canDefault, picked);
        flyout.ShowAt(anchor);
    }

    // A compact list, like a menu: the current value is highlighted, a click or Enter picks.
    private static ListBox Choices(Flyout flyout, ChoicePicker model, Action<string?> picked)
    {
        var list = new ListBox { ItemsSource = model.Labels, MinWidth = 120, SelectedIndex = model.SelectedIndex };

        void Pick()
        {
            if (list.SelectedIndex < 0)
                return;
            picked(model.TextAt(list.SelectedIndex));
            flyout.Hide();
        }

        list.Tapped += (_, _) => Pick();
        list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Pick();
                e.Handled = true;
            }
        };
        flyout.Opened += (_, _) => list.Focus();
        return list;
    }

    private static StackPanel Moment(Flyout flyout, MomentPicker model, bool canDefault, Action<string?> picked)
    {
        var day = Combo(model.Days, model.DayIndex, 70);
        var syncing = false;

        void Wire(ComboBox box, Action<int> set, bool refitDays = false) => box.SelectionChanged += (_, _) =>
        {
            if (syncing || box.SelectedIndex < 0)
                return;
            set(box.SelectedIndex);
            if (refitDays)
            {
                syncing = true;
                day.ItemsSource = model.Days;
                day.SelectedIndex = model.DayIndex;
                syncing = false;
            }

            picked(model.Text);
        };

        var month = Combo(model.Months, model.MonthIndex, 130);
        var year = Combo(model.Years, model.YearIndex, 90);
        var hour = Combo(model.Hours, model.Hour, 70);
        var minute = Combo(model.Sixty, model.Minute, 70);
        var second = Combo(model.Sixty, model.Second, 70);
        Wire(day, i => model.DayIndex = i);
        Wire(month, i => model.MonthIndex = i, true);
        Wire(year, i => model.YearIndex = i, true);
        Wire(hour, i => model.Hour = i);
        Wire(minute, i => model.Minute = i);
        Wire(second, i => model.Second = i);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Row(day, month, year));
        if (model.HasTime)
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

    private static ComboBox Combo(IReadOnlyList<string> items, int selected, double width) =>
        new() { ItemsSource = items, SelectedIndex = selected, MinWidth = width };

    private static StackPanel Row(params ComboBox[] boxes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var box in boxes)
            row.Children.Add(box);
        return row;
    }
}
