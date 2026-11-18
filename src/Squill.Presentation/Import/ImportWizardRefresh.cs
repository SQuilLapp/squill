namespace Squill.Presentation.Import;

/// <summary>The parts of the wizard window a view-model property change must redraw.</summary>
[Flags]
public enum ImportWizardRefresh
{
    /// <summary>Nothing to redraw.</summary>
    None = 0,

    /// <summary>The page of the current step.</summary>
    Page = 1,

    /// <summary>The step rail on the left.</summary>
    Rail = 2,

    /// <summary>The footer: Back and Next buttons and the summary.</summary>
    Footer = 4,

    /// <summary>The file name, file information and preview of the source step.</summary>
    Source = 8,

    /// <summary>The encoding, delimiter, quote and header controls of the source step.</summary>
    SourceOptions = 16,

    /// <summary>The list of target tables.</summary>
    Tables = 32,

    /// <summary>The environment pill.</summary>
    Pill = 64,

    /// <summary>The warning band and the issues list.</summary>
    Warnings = 128,

    /// <summary>The options step: large file hint and rejects file.</summary>
    Options = 256,

    /// <summary>The progress counters, rate and outcome of the run step.</summary>
    Run = 512,
}

/// <summary>Maps the property names raised by <see cref="ImportWizardViewModel"/> to the window parts to redraw.</summary>
public static class ImportWizardRefreshMap
{
    /// <summary>Gets what to redraw after a property changed.</summary>
    /// <param name="propertyName">The name carried by <c>PropertyChanged</c>.</param>
    /// <returns>The parts to redraw, or <see cref="ImportWizardRefresh.None"/> for a property the window does not show.</returns>
    public static ImportWizardRefresh For(string? propertyName) => propertyName switch
    {
        nameof(ImportWizardViewModel.Step) => ImportWizardRefresh.Page | ImportWizardRefresh.Footer,
        nameof(ImportWizardViewModel.Steps) => ImportWizardRefresh.Rail,
        nameof(ImportWizardViewModel.CanGoNext) or nameof(ImportWizardViewModel.CanGoBack) or nameof(ImportWizardViewModel.IsRunning)
            or nameof(ImportWizardViewModel.MappingSummary) or nameof(ImportWizardViewModel.FileSummary)
            => ImportWizardRefresh.Footer | ImportWizardRefresh.Source | ImportWizardRefresh.Run,
        nameof(ImportWizardViewModel.Preview) or nameof(ImportWizardViewModel.FilePath) or nameof(ImportWizardViewModel.ColumnsText)
            => ImportWizardRefresh.Source,
        nameof(ImportWizardViewModel.EncodingName) or nameof(ImportWizardViewModel.Delimiter) or nameof(ImportWizardViewModel.Quote)
            or nameof(ImportWizardViewModel.HasHeader)
            => ImportWizardRefresh.SourceOptions,
        nameof(ImportWizardViewModel.TableNames) => ImportWizardRefresh.Tables,
        nameof(ImportWizardViewModel.Environment) => ImportWizardRefresh.Pill,
        nameof(ImportWizardViewModel.WarningText) or nameof(ImportWizardViewModel.Issues) => ImportWizardRefresh.Warnings,
        nameof(ImportWizardViewModel.LargeFileHint) or nameof(ImportWizardViewModel.RejectsPath) => ImportWizardRefresh.Options,
        nameof(ImportWizardViewModel.Read) or nameof(ImportWizardViewModel.Inserted) or nameof(ImportWizardViewModel.Rejected)
            or nameof(ImportWizardViewModel.Fraction) or nameof(ImportWizardViewModel.RateText) or nameof(ImportWizardViewModel.RemainingText)
            or nameof(ImportWizardViewModel.Summary) or nameof(ImportWizardViewModel.Result) or nameof(ImportWizardViewModel.HasRejects)
            => ImportWizardRefresh.Run | ImportWizardRefresh.Footer,
        _ => ImportWizardRefresh.None,
    };
}
