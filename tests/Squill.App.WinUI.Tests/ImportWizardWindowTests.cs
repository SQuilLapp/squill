using Xunit;

namespace Squill.App.WinUI.Tests;

public class ImportWizardWindowTests
{
    private static string Code => File.ReadAllText("Xaml/ImportWizardWindow.cs");

    [Fact]
    public void Combos_hold_plain_strings_added_one_by_one_and_never_an_items_source()
    {
        var code = Code;

        Assert.DoesNotContain("ItemsSource", code);
        Assert.Contains("box.Items.Add(item)", code);
        Assert.Contains("choices.Prepend(UiText.Get(\"import.ui.ignore\"))", code);
        Assert.Contains("row.Target = i <= 0 ? null : choices[i - 1]", code);
    }

    [Fact]
    public void The_run_starts_only_from_the_explicit_button_never_from_a_key()
    {
        var code = Code;

        Assert.DoesNotContain("VirtualKey.Enter", code);
        Assert.DoesNotContain("IsDefault", code);
        Assert.Contains("_vm.Step != ImportStep.Options && _vm.CanGoNext", code);
        Assert.Contains("\"import.ui.start\"", code);
    }

    [Fact]
    public void The_window_binds_the_view_model_commands_and_builds_one_page_per_step()
    {
        var code = Code;

        Assert.Contains("_vm.NextCommand.ExecuteAsync", code);
        Assert.Contains("_vm.BackCommand", code.Replace("_vm.Back()", "_vm.BackCommand"));
        Assert.Contains("ImportStep.Source => SourcePage()", code);
        Assert.Contains("ImportStep.Target => TargetPage()", code);
        Assert.Contains("ImportStep.Mapping => MappingPage()", code);
        Assert.Contains("ImportStep.Options => OptionsPage()", code);
        Assert.Contains("foreach (var row in _vm.Rows)", code);
        Assert.Contains("SquillWarningSoftBrush", code);
    }

    [Fact]
    public void The_dialog_service_opens_the_window_and_picks_files()
    {
        var code = File.ReadAllText("Xaml/WinUiDialogService.cs");

        Assert.Contains("ShowImportWizardAsync", code);
        Assert.Contains("new Views.ImportWizardWindow(", code);
        Assert.Contains("FileOpenPicker", code);
    }
}
