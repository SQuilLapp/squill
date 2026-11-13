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

    private static Grid Moment(Flyout flyout, MomentPicker model, bool canDefault, Action<string?> picked)
    {
        var boxes = Enumerable.Range(0, model.BoxCount).Select(i => Combo(model.ItemsOf(i), model.IndexOf(i), i == 1 ? 140 : 80)).ToList();
        var layout = new Grid { RowSpacing = 8, ColumnSpacing = 8 };
        for (var row = 0; row < (model.HasTime ? 2 : 1) + (canDefault ? 1 : 0); row++)
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column < 3; column++)
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var refitting = false;
        foreach (var (box, at) in boxes.Select((b, i) => (b, i)))
        {
            Grid.SetRow(box, at / 3);
            Grid.SetColumn(box, at % 3);
            layout.Children.Add(box);
            box.SelectionChanged += (sender, _) =>
            {
                var chosen = ((ComboBox)sender).SelectedIndex;
                if (refitting || chosen < 0)
                    return;
                if (model.Select(at, chosen))
                {
                    refitting = true;
                    boxes[0].ItemsSource = model.Days.ToArray();
                    boxes[0].SelectedIndex = model.DayIndex;
                    refitting = false;
                }

                picked(model.Text);
            };
        }

        if (canDefault)
        {
            var useDefault = new Button { Content = CellEditorKinds.DefaultLabel, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            Grid.SetRow(useDefault, model.HasTime ? 2 : 1);
            Grid.SetColumnSpan(useDefault, 3);
            useDefault.Click += (_, _) =>
            {
                flyout.Hide();
                picked(null);
            };
            layout.Children.Add(useDefault);
        }

        return layout;
    }

    private static ComboBox Combo(IReadOnlyList<string> items, int selected, double width) =>
        new() { ItemsSource = items.ToArray(), SelectedIndex = selected, MinWidth = width };
}
