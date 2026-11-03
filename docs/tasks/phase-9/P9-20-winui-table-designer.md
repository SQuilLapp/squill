# P9-20 - WinUI head: table designer view

Status: done
Depends on: P9-10, P9-02

## Goal
The WinUI table designer matches M07.

## Design decisions (final)
- Same content rules as P9-19 for `Views/TableDesignerView.xaml`; DDL panel highlighting with the highlighter the head already uses for scripts (read `Editors/`; plain mono text if none exists, and say so).

## Out of scope
- Impact card, extra tabs, rollback option (no feature).

## Tasks
- [x] Rework `TableDesignerView.xaml(.cs)`.
- [x] Tests for the testable helpers; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M07-Table-clair-Windows.png`, `M07-Table-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: table designer view

## Report
- Visual check on the published AOT exe (dark theme, window 1440 x 900 logical = 2160 x 1350 px, `commandes` of the sample `ventes.db`) against M07; compared by eye with the Compare-Mockup output. The light theme could not be captured: the app kept the dark theme after `settings.json` was switched, so the owner must check light by hand. Row tags `MODIFIÉE`/`NOUVELLE`, the draft state, the environment pill and the issue badges were not exercised in the live window (no edit was driven); they are bound to the same view-model state as P9-19.
- Not drawn (no data or feature): `Commentaire` column, `Impact estimé`, Contraintes/Déclencheurs/DDL tabs, DDL footer checkboxes. At 1440 px the columns table is narrow because both side panels are open; columns are 30/40/*/120/54/100/80.
- The DDL preview uses the head's SQL editor (`EditorFactory`: LoomEditor), made read-only through a new `IEditorControl.IsReadOnly`. Sub-tabs use `Pivot` with a 13 px `PivotHeaderItem` style; counts are not mono-muted per tab beyond the count text.
- Bug found and worked around: `ItemsSource = IReadOnlyList<DesignIssue>` (an empty collection expression) throws E_INVALIDARG under AOT and crashed the app when a designer opened; the issue list is now given as a `List`.
- Third-party IL2104/IL3053 warnings (Microsoft.Data.SqlClient, System.Configuration.ConfigurationManager) come from NuGet assemblies, none from SQuilL code.
