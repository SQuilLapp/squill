using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Squill.Presentation.Editing;

namespace Squill.App.WinUI.Controls;

/// <summary>The popup that helps to fill a boolean, a date or a date with a time; the text box stays editable by hand.</summary>
internal static class CellValuePicker
{
    /// <summary>Opens the popup under the text box being edited, when a cell is being edited.</summary>
    /// <param name="anchor">The text box being edited.</param>
    /// <param name="edit">The edit in progress; the popup writes its choice there.</param>
    /// <param name="focusGrid">Gives the focus back to the grid when a choice ended the edit.</param>
    public static void Show(FrameworkElement anchor, GridEditController? edit, Action focusGrid)
    {
        if (edit is not { IsEditing: true } c)
            return;
        var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
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

    // A compact list, like a menu: the current value is highlighted, a click picks.
    private static ListView Choices(Flyout flyout, ChoicePicker model, Action<string?> picked)
    {
        var list = new ListView { ItemsSource = model.Labels.ToArray(), MinWidth = 140, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, SelectedIndex = model.SelectedIndex };
        list.ItemClick += (_, e) =>
        {
            picked(model.TextAt(list.Items.IndexOf(e.ClickedItem)));
            flyout.Hide();
        };
        return list;
    }

    private static StackPanel Moment(Flyout flyout, MomentPicker model, bool canDefault, Action<string?> picked)
    {
        var boxes = Enumerable.Range(0, model.BoxCount).Select(i => Combo(model.ItemsOf(i).ToArray(), model.IndexOf(i), i == 1 ? 140 : 80)).ToList();
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
                    boxes[0].ItemsSource = model.Days.ToArray();
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
        new() { ItemsSource = items.ToArray(), SelectedIndex = selected, MinWidth = width };

    private static StackPanel Row(IEnumerable<ComboBox> boxes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var box in boxes)
            row.Children.Add(box);
        return row;
    }
}
