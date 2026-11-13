using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Squill.Presentation.Editing;

namespace Squill.App.Avalonia.Controls;

/// <summary>The popup that helps to fill a boolean, a date or a date with a time; the text box stays editable by hand.</summary>
internal static class CellValuePicker
{
    /// <summary>Opens the popup under the text box being edited, when a cell is being edited.</summary>
    /// <param name="anchor">The text box being edited.</param>
    /// <param name="edit">The edit in progress; the popup writes its choice there.</param>
    /// <param name="focusGrid">Gives the focus back to the grid when a choice ended the edit.</param>
    public static void Show(Control anchor, GridEditController? edit, Action focusGrid)
    {
        if (edit is not { IsEditing: true } c)
            return;
        var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        void Picked(string? text)
        {
            if (c.Pick(text))
                focusGrid();
        }

        flyout.Content = c.EditorKind == CellEditorKind.Boolean
            ? Choices(flyout, new ChoicePicker(c.EditText, c.CanUseDefault), Picked)
            : Moment(flyout, new MomentPicker(c.EditorKind, c.EditText), c.CanUseDefault, Picked);
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
        var boxes = Enumerable.Range(0, model.BoxCount).Select(i => Combo(model.ItemsOf(i), model.IndexOf(i), i == 1 ? 130 : 70)).ToList();
        var syncing = false;
        for (var i = 0; i < boxes.Count; i++)
        {
            var (at, box) = (i, boxes[i]);
            box.SelectionChanged += (_, _) =>
            {
                if (syncing || box.SelectedIndex < 0)
                    return;
                if (model.Select(at, box.SelectedIndex))
                {
                    syncing = true;
                    boxes[0].ItemsSource = model.Days;
                    boxes[0].SelectedIndex = model.DayIndex;
                    syncing = false;
                }

                picked(model.Text);
            };
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Row(boxes.Take(3)));
        if (model.HasTime)
            panel.Children.Add(Row(boxes.Skip(3)));
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

    private static StackPanel Row(IEnumerable<ComboBox> boxes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var box in boxes)
            row.Children.Add(box);
        return row;
    }
}
