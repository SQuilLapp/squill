# P9-22 - WinUI head: ER diagram view

Status: done
Depends on: P9-10, P9-02

## Goal
The WinUI ER diagram tab matches M08.

## Design decisions (final)
- Same content rules as P9-21 for `Views/DiagramTabView.xaml` and `Controls/ErDiagramCanvas.cs` (Win2D, bundled JetBrains Mono through P9-01).

## Out of scope
- Layout modes not in the engine, PNG/PDF/Mermaid export (no feature).

## Tasks
- [x] Rework the view and the canvas drawing.
- [x] Tests for the testable geometry; state what needs the screenshot.

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M08-ER-clair-Windows.png`, `M08-ER-sombre-Windows.png` through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
WinUI: ER diagram view

## Report
- Visual check done in light and dark (AOT exe, sample profile, composites `p22-light-cmp.png` / `p22-dark-cmp.png` in `%TEMP%\squill-visual`), driven with UI Automation plus real mouse clicks (connect, rail diagram button). Not drawn on purpose (no data or feature), same as P9-21: row counts in node headers, `Commentaires` and `Clés seulement si replié` checkboxes, layout modes, table-list visibility checkboxes (plain list focusing the table), PNG/PDF/Mermaid export. The relation tooltip (FK name and columns, no referential actions: the model lacks them) and hover states were not driven: the owner should hover a relation by hand. The `Exporter` menu item text and the 1/2/3 hops tooltip were not checked in the running app (the tooltip is plain French text, not from the catalog).
- Visible differences left: the capture window content area is narrower than 1440 logical px (the toolbar fits only because the segmented buttons were narrowed), and the explorer is open while the mockup shows it closed.
- Win2D needs an array for `ListView.ItemsSource` under AOT (an `IReadOnlyList` crashes with E_INVALIDARG), and `Checked` handlers must be wired after `InitializeComponent` (they fire during load before the canvas exists).
- The Native AOT publish prints only IL2104/IL3053 warnings from the third-party assemblies Microsoft.Data.SqlClient and System.Configuration.ConfigurationManager, none from SQuilL code.
