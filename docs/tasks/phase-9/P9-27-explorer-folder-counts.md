# P9-27 - Explorer: object counts on schema and folder rows

Status: done
Depends on: P9-26

## Goal
Schema and folder rows of the object explorer show a right-aligned count in both heads, as in M03 (`docs/design/visual-spec.md` section 3: "counts right-aligned `fg-3`"). P9-08 and P9-10 left this out because `ExplorerNode` carries no count.

## Design decisions (final)
- Counts come from the schema snapshot `ExplorerTreeViewModel` already holds (`session.Schema.Tables`). No new query, no new Core/Data type.
- `ExplorerNode` (`src/Squill.Presentation/Explorer/ExplorerNode.cs`) gains a constructor parameter `int? count = null` (after `table`, so existing call sites keep compiling), a read-only `int? Count` property and a read-only `string? Trailing` property: `Detail` when it is not null, otherwise `Count` formatted with `CultureInfo.CurrentCulture` (`"N0"`, so 1208 shows as `1 208` in French), otherwise `null`. Both XML-documented; `Trailing` says it is the one text the heads draw right-aligned.
- Counts set by `ExplorerTreeViewModel.Populate`/`TableNode`:
  - `Schema` node: number of tables plus views in the schema (its direct children).
  - `Columns`, `Indexes`, `Foreign keys` folders: number of their children, drawn even when 0 (uniform rows; mention in the report if M03 differs).
  - Table, view, column, index and foreign-key rows keep their `Detail` and have no `Count`.
- Counts are static: they do not change with `FilterText` (the filter only toggles `IsVisible`). Say so in the `Count` XML doc.
- Heads bind `Trailing` instead of `Detail`: `src/Squill.App.WinUI/Views/SidebarView.xaml` (the `TextBlock` in column 2 of the tree item template) and `src/Squill.App.Avalonia/Views/ObjectExplorerView.axaml` (line with `{Binding Detail}`). Keep the existing mono style, `fg-3` brush, 8 px left margin and right alignment; add no new column, no new control, no code-behind.

## Out of scope
- Counts on connection rows, connection folders or the connection manager group rows (those exist from P9-04/P9-17/P9-18).
- Tree row height changes (the single tree and the `Tables`/`Vues` folders came with P9-26).
- Lazy loading or live count refresh (the tree is rebuilt from the snapshot on refresh, which recomputes the counts).

## Tasks
- [x] `ExplorerNode`: add `Count` and `Trailing` as described; keep `Detail` unchanged.
- [x] `ExplorerTreeViewModel`: pass counts for the `Tables`/`Vues` folders (P9-26 added them) and the Columns/Indexes/Foreign keys folders in `Populate`/`TableNode`; the schema node count is its tables plus views.
- [x] WinUI `SidebarView.xaml` and Avalonia `ObjectExplorerView.axaml`: bind `Trailing`.
- [x] Headless tests in `tests/Squill.Presentation.Tests/Explorer/ExplorerTreeViewModelTests.cs` (extend the existing fake-schema helpers): a two-schema schema gives each `Schema` node a count equal to its tables plus views; the `Columns`, `Indexes` and `Foreign keys` folders of a table give their child counts, including a table with no index (count 0); table, view and column nodes have `Count == null`; `Trailing` returns `Detail` when set, the formatted count otherwise, and `null` when neither; the filter does not change counts; `RefreshCommand` after the fake session schema changes recomputes counts. Use a fixed culture (`CultureInfo.InvariantCulture`) in the assertions or set it for the test.
- [x] No head-specific tests: the binding change is checked by the screenshot (say so in the report).

## Acceptance criteria
- `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings (and `Squill.Windows.slnx` for the WinUI head).
- `rtk bypass dotnet test --solution Squill.slnx -c Release` and `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` are green (new tests included, none skipped except Docker-gated ones).
- `rtk dotnet publish src/Squill.App.Avalonia -c Release -r win-x64` and `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeed with zero IL2xxx/IL3xxx warnings.
- Visual check (`docs/design/visual-spec.md` section 11): the running app (both heads, a connection with several schemas and one with a single schema, both from the sample data of P9-02) was captured in light and dark with `tools/visual/Capture-Window.ps1`, compared with `M03-Workspace-*-Windows.png` (explorer counts) through `tools/visual/Compare-Mockup.ps1`, and every visible difference was fixed or listed in the card report with its reason. If a state could not be reached or driven, the report says which and what the owner must check by hand.
- No process left running (app, `dotnet build-server`, AOT executable).

## Suggested commit message
Explorer: object counts on schema and folder rows
