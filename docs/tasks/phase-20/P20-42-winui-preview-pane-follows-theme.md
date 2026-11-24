# P20-42 - WinUI: the preview pane follows the theme (light theme drew it dark with near-invisible text)

Status: todo
Depends on: P20-40

## Goal
Bug found by the P20-40 drive (Done notes, "Found, not fixed") on the WinUI AOT exe, light theme / English: the cell preview pane (beside the result grid) is drawn dark, its text light and hard to read, while the rest of the window is light. Make every brush of the pane follow the theme the pane is shown in, at start and when the theme changes in the settings, guard the class of bug with a test, and capture both themes on the AOT exe. Check the Avalonia pane too.

Files today: `src/Squill.App.WinUI/Views/CellPreviewPanel.cs`, `src/Squill.App.WinUI/Views/Tokens.cs`, `src/Squill.App.WinUI/Views/QueryTabView.cs` (creates `_preview` and shows the repaint pattern: `Loaded` and `ActualThemeChanged` -> `PaintResults`, brushes from `Tokens.Brush(this, key)`), `src/Squill.App.WinUI/MainWindow.xaml.cs` (`ApplyTheme`), `tests/Squill.App.WinUI.Tests/ThemeLookupScanTests.cs` (P20-02 guard), Avalonia `src/Squill.App.Avalonia/Views/CellPreviewPanel.cs`.

## Root cause (read in the code at `0cf55e5`)
1. `CellPreviewPanel` takes every brush once, in field initializers and the constructor, through its `private static Brush Brush(string key) => Tokens.Brush(key)`: the **element-less** `Tokens.Brush(string)`, which reads the variant from the static `Tokens.Active`.
2. The pane is built while the main window is built: `MainWindow.xaml` declares `<v:QueryTabView x:Name="ActiveTabView" />`, whose field `_preview = new()` runs inside `InitializeComponent()`. `Tokens.Active` is set later, by `ApplyTheme` (from `Attach`, when the settings load) and by `Root.ActualThemeChanged`. At construction it is still `ElementTheme.Default`, and `Tokens.Brush(string)` maps everything but `Light` to the `Default` (dark) token dictionary. So the pane gets the dark `SquillBgSurface`, `SquillBgBase`, `SquillFg`, `SquillFg3`, `SquillBorderSubtle` and `SquillDanger` colors whatever the chosen theme.
3. Nothing repaints it: unlike `QueryTabView`, `FilterBar`, `SearchBox`, `CommandPalette` and `Splitter`, `CellPreviewPanel` has no `ActualThemeChanged` / `Loaded` handler, and `QueryTabView.PaintResults` does not touch `_preview`. The light theme (and any theme switch at run time) keeps the dark brushes.
4. The P20-02 guard (`ThemeLookupScanTests`) only bans `Application.Current.Resources[...Brush]` lookups; an element-less `Tokens.Brush(string)` in a view built before the theme is known, with no repaint, is the same class of bug and is not caught. Related precedent: P20-22 (a `TextBox` template state resolved its text against the wrong theme; fixed with its own template and a style test).
5. Not a cause, check it in the drive: the stock WinUI `TextBox` template swaps its border background to `TextControlBackgroundPointerOver` / `TextControlBackgroundFocused` (`SquillBgRaised`, `Styles/ControlResources.xaml`, merged in both theme dictionaries, so it follows the theme) and its text to `TextControlForegroundFocused` while the pane's box has focus. P20-22 saw the stock template force the light theme on a focused box's text; that would give dark text on the dark box. Look at the focused pane in both themes.

## Design decisions (final)
- **Pane**: drop the static `Brush(string)` helper and every brush from the field initializers. A `private void Paint()` sets, through `Tokens.Brush(this, key)` (the element's `ActualTheme` decides), `_title.Foreground` (`SquillFgBrush`), `_type.Foreground` (`SquillFg3Brush`), `_error.Foreground` (`SquillDangerBrush`), `_text.Background` (`SquillBgBaseBrush`), `_text.Foreground` (`SquillFgBrush`), and the frame `Border` (keep it in a field) `Background` (`SquillBgSurfaceBrush`) and `BorderBrush` (`SquillBorderSubtleBrush`). Call it at the end of the constructor, on `Loaded` and on `ActualThemeChanged`, as `QueryTabView` does (lines ~198-199). The `PrimaryButtonStyle` and `SquillFontMono` lookups are not brushes and stay. If P20-34 already added a working strip (`ProgressBar`) to the pane, it uses the default style; give it a brush in `Paint` only if the capture shows it wrong.
- **Focused box**: only if the drive shows the focused or pointer-over pane box unreadable or with a different background than unfocused in either theme, set the lightweight keys on the box in `Paint`: `_text.Resources["TextControlBackground" | "...PointerOver" | "...Focused"]` = the `SquillBgBaseBrush` of `Paint`, `_text.Resources["TextControlForeground" | "...PointerOver" | "...Focused"]` = its `SquillFgBrush` (as `SearchBox` does with its transparent keys). If the focused text is still in the wrong theme after that (the template forces a theme on its content), stop and propose a card fix with P20-22's remedy (an own `TextBox` template in a style) rather than improvising one in code.
- **Guard** (`ThemeLookupScanTests`, new fact `Element_less_token_brushes_are_repainted`): every `.cs` file under `src/Squill.App.WinUI` (same walk and `bin` / `obj` exclusions as the existing fact; `Tokens.cs` allowed) that contains `Tokens.Brush("` or `=> Tokens.Brush(key)` must also contain `ActualThemeChanged`; the message lists the offenders. Today it fails on `Views/CellPreviewPanel.cs` only (`CommandPalette.cs`, `FilterBar.cs` and `QueryTabView.cs` repaint). One-line comment `// shortcut: text scan, it does not prove the repaint covers every brush; upgrade to an analyzer if it misses one.`
- **Avalonia**: its pane binds every brush with `GetResourceObservable` (follows the theme), and the Fluent `TextBox` state keys are aliased to Squill brushes in `Styles/ControlAliases.axaml`. No change expected; capture both themes to confirm, and fix only what the capture shows wrong (with a headless test on the resolved brush if a fix is needed).
- No `Application.Current.Resources[...]` brush lookup, no new package, no new UI text.

## Out of scope
- Other views built with element-less lookups that already repaint; the pane's text rules (P20-41); the long-paste strip itself (P20-34 / P20-35); a dark theme over a light system (cannot switch the system, as P20-15).

## Tasks
- [ ] Tests first (see them fail):
  - `ThemeLookupScanTests.Element_less_token_brushes_are_repainted` as above.
  - `tests/Squill.App.WinUI.Tests/CellPreviewPanelTests.cs` (text checks; link `Views\CellPreviewPanel.cs` as `Content`, `Link="Xaml\CellPreviewPanel.cs"`; reuse the class and link if P20-34 or P20-41 already added them): `Pane_paints_from_its_own_theme` (contains `ActualThemeChanged`, `Loaded`, `Tokens.Brush(this, "SquillBgSurfaceBrush")`, `Tokens.Brush(this, "SquillFgBrush")`; does not contain `Tokens.Brush(key)` nor `Tokens.Brush("`).
- [ ] Implement `Paint` and its three calls; remove the static helper.
- [ ] Publish the WinUI AOT head and drive it (P20-01 `Drive-Window.ps1`), SQLite `New-VisualProfile.ps1 -Scenario Full` in the scratchpad, a cell that opens the pane (a JSON or long value of `clients`, or store one as in P20-40 step 1). Captures:
  1. light / English, app started in light: pane unfocused, then focused (click into the text), then with the error line (delete one `}` of a JSON value); text, title, type and error readable, background light.
  2. dark / French: the same three captures (no regression).
  3. theme switched at run time in the settings, light -> dark and back, pane open: it repaints without reopening.
  Zoom on the pane (`computer zoom` or a cropped capture) and compare the pane background with the grid background; write what each capture shows.
- [ ] Publish the Avalonia AOT head; captures 1 and 2 if the driver can select a cell that opens the pane (P20-40 could not open the editor on the canvas grid), otherwise `not driven (<why>)`.
- [ ] Read every PNG; captures are not committed. Close the apps, `dotnet build-server shutdown`.
- [ ] Set the P20-42 row of `docs/tasks/README.md` to `done` and write the Done notes (the root cause as confirmed, the focused-box result in both themes, the Avalonia result).

## Acceptance criteria
- Recipe 2 common acceptance (as P20-40) with `Squill.Windows.slnx`, the WinUI AOT publish (`eng/check-aot-warnings.ps1 -Head WinUI`, baseline warnings only), the app starts, captures read (light and dark).
- In the light theme the WinUI preview pane is light with readable text, focused or not; in the dark theme it is unchanged; a theme switch repaints it; the new guard is green and would have caught this bug.

## Suggested commit message
Paint the WinUI preview pane with the brushes of its own theme
