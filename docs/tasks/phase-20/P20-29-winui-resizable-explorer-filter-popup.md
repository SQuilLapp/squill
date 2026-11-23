# P20-29 - WinUI: a resizable explorer filter popup, its size kept for the session

Status: done
Depends on: P20-27, P20-28

## Goal
Owner feedback after testing the WinUI AOT build of P20-27: "pretty good but the popup should be resizable". Give the explorer filter popup a bottom-right resize grip, keep the chosen size for the rest of the session and reuse it when the popup reopens, keep the popup inside the window (P20-27 Done notes: near the window bottom it is clipped by the window edge), and let the row list grow and shrink with the popup. This card adds the size rules shared by both heads (Presentation) and the WinUI head; P20-30 brings the same to Avalonia.

Files today: `src/Squill.App.WinUI/Views/ExplorerFilterMenu.cs` (`Flyout`, placement `BottomEdgeAlignedLeft`, presenter style padding 0 and `MaxWidth` 600, content a `Grid` of width `MenuWidth = 440` with three `Auto` rows: tool bar, search box, `ScrollViewer` with `MaxHeight = ListHeight = 420` around the row `StackPanel`), its callers in `Views/SidebarView.xaml.cs` (`ShowFilter(node, anchor)`: the anchor is the badge button or the row that raised the context menu), and the pointer-capture precedent `src/Squill.App.WinUI/Controls/Splitter.cs` (`UserControl`, `ProtectedCursor`, `CapturePointer`, `PointerMoved` while the left button is pressed).

## Design decisions (final)
- **The popup stays a `Flyout`.** A WinUI `Flyout` sizes to its content, so resizing it means changing the content's `Width` / `Height`; nothing in the flyout prevents that. Two presenter limits must be lifted in the presenter style built in `Show`: `MaxWidth` and `MaxHeight` set to `double.PositiveInfinity` (the default `FlyoutPresenter` style caps the height at `FlyoutThemeMaxHeight`, and the template's own `ScrollViewer` would then scroll the whole popup). No other container is needed; if the flyout still refuses a size (presenter scrolls or clips), stop and report rather than switching to a `Popup`.
- **Shared size rules** (Presentation, no UI type): new `src/Squill.Presentation/Explorer/ExplorerFilterPopupSize.cs`, a `public readonly record struct ExplorerFilterPopupSize(double Width, double Height)` with XML docs:
  - `Default` = 440 x 520 (the list keeps about its P20-27 height of 420), `Minimum` = 320 x 220, `const double WindowMargin = 8`.
  - `static ExplorerFilterPopupSize Fit(ExplorerFilterPopupSize wanted, double maxWidth, double maxHeight)`: each side clamped to `[Minimum, max]`; when the window is smaller than the minimum, the maximum wins (never larger than the space; `Math.Clamp` would throw, use `Math.Max(Math.Min(v, max), Math.Min(min, max))` and treat a negative `max` as 0).
  - `static bool OpensAbove(double height, double spaceBelow, double spaceAbove)`: `true` when `height > spaceBelow && spaceAbove > spaceBelow`.
- **Remembered size, in memory only**: `ExplorerFilterMenu` keeps `private static ExplorerFilterPopupSize? s_size`, set at the end of each drag; `Show` uses `s_size ?? ExplorerFilterPopupSize.Default`. One size for every level (connection, database, schema popups alike), lost at restart. **Not persisted** in this card: no window, sidebar or splitter size is saved today (`AppSettings` holds only preferences), so saving this one alone would be the odd one out. If the owner wants it later, the way that fits the conventions: two `double` parameters `ExplorerFilterWidth = 0` and `ExplorerFilterHeight = 0` at the end of `AppSettings` (0 = default), clamped in `AppSettings.Clamp()`, written through the existing `ISettingsStore` / `JsonSettingsStore` (source-generated `SettingsJsonContext`, nothing to add) once on drag end; best done in the same card that saves the window and sidebar sizes.
- **Layout** (`Build`): the content `Grid` gets `Width` / `Height` from the fitted size and four rows: tool bar `Auto`, search box `Auto`, list `*`, grip row `Auto`. The `ScrollViewer` loses its `MaxHeight` and fills the `*` row, so the list grows with the popup and scrolls when the rows do not fit. Remove `ListHeight`; `MenuWidth` becomes `ExplorerFilterPopupSize.Default.Width`.
- **Grip**: new `src/Squill.App.WinUI/Views/ResizeGrip.cs`, `internal sealed partial class ResizeGrip : UserControl` (one-line `<summary>`), built in code like `Splitter`: 14 x 14, `HorizontalAlignment.Right`, margin `0,0,2,2`, `ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast)`, content a `Canvas` with two short diagonal `Microsoft.UI.Xaml.Shapes.Line`s (the classic corner grip), stroke `Tokens.Brush(this, "SquillFg3Brush")` painted on `Loaded` and `ActualThemeChanged` like `Splitter`; `AutomationProperties.Name` = new key `explorer.filter.resize` ("Resize" / "Redimensionner", fr and en catalogs). Constructor takes the panel it resizes and an `Action<ExplorerFilterPopupSize>` called on release (stores `s_size`).
  - `PointerPressed`: capture the pointer, keep the start pointer (`e.GetCurrentPoint(null).Position`), the start size (`panel.ActualWidth` / `ActualHeight`) and the limits: the panel's top-left in the window (`panel.TransformToVisual(null).TransformPoint(default)`), `maxWidth = XamlRoot.Size.Width - left - WindowMargin`, `maxHeight = XamlRoot.Size.Height - top - WindowMargin`; mark handled (the flyout must not take it as a light dismiss).
  - `PointerMoved` with the left button pressed: `Fit(start + delta, maxWidth, maxHeight)` into `panel.Width` / `Height`.
  - `PointerReleased` / `PointerCaptureLost`: release, report the size.
  - Edge resizing (left, top, right, bottom bars) is not built: the corner grip covers both directions, and the left and top edges would need moving the popup.
- **Opening inside the window** (`Show`): measure the anchor in the window (`anchor.TransformToVisual(null).TransformBounds(new Rect(0, 0, anchor.ActualWidth, anchor.ActualHeight))`, root `anchor.XamlRoot.Size`); `spaceBelow = root.Height - bounds.Bottom - WindowMargin`, `spaceAbove = bounds.Top - WindowMargin`; `above = OpensAbove(wanted.Height, spaceBelow, spaceAbove)`; size = `Fit(wanted, root.Width - bounds.Left - WindowMargin, above ? spaceAbove : spaceBelow)`; placement `TopEdgeAlignedLeft` when `above`, else `BottomEdgeAlignedLeft`. Nothing is clipped at the window bottom any more: the popup either fits below, opens above, or shrinks (the list scrolls). A drag after opening above grows the popup downwards (WinUI keeps the popup's top-left), still limited by the window bottom.
- Brushes through `Tokens.Brush(element, key)` only (`ThemeLookupScanTests`), no `ItemsSource` (Native AOT trap, P20-27).

## Out of scope
- The Avalonia head (P20-30), saving the size in settings, edge grips, resizing the popup when the window itself is resized while it is open (the next open fits it again), view-model changes.

## Tasks
- [x] Tests first:
  - `tests/Squill.Presentation.Tests/Explorer/ExplorerFilterPopupSizeTests.cs`: `Fit_WithinLimits_KeepsTheSize`; `Fit_TooLarge_ClampsToTheSpace`; `Fit_TooSmall_ClampsToTheMinimum`; `Fit_WindowSmallerThanMinimum_TheSpaceWins` (max 200 x 100 gives 200 x 100); `Fit_NegativeSpace_GivesZero`; `OpensAbove_FitsBelow_False`; `OpensAbove_MoreRoomAbove_True`; `OpensAbove_MoreRoomBelow_False`.
  - `tests/Squill.App.WinUI.Tests/ExplorerFilterPopupTests.cs` (text checks, link `Views\ResizeGrip.cs` as `Content` `Xaml\ResizeGrip.cs` in the csproj like `ExplorerFilterMenu.cs`): `ExplorerFilterMenu.cs` contains `ResizeGrip`, `ExplorerFilterPopupSize.Fit`, `OpensAbove`, `TopEdgeAlignedLeft`, `PositiveInfinity` and no longer `MaxHeight = ListHeight`; `ResizeGrip.cs` contains `SizeNorthwestSoutheast` and `CapturePointer`, and neither file contains `ItemsSource` or `Application.Current.Resources`.
  - See them fail, then implement; catalog parity and used-keys tests stay green with `explorer.filter.resize`.
- [x] Implement the size type, the grip, the layout and the placement.
- [x] Publish the WinUI AOT head and drive it (P20-01 `Drive-Window.ps1`, procedure of recipe 3 of [verification-recipe.md](verification-recipe.md)), SQLite `-Scenario Full` is enough (PostgreSQL `pg-visual` optional):
  - open the popup from a schema badge: default size, list about as tall as in P20-27;
  - drag the grip right and down (about +200 x +150): the popup and the list grow, the rows reflow, no scroll bar on the presenter itself; drag beyond the window bottom-right: the popup stops 8 px inside the window; drag up-left far: it stops at 320 x 220;
  - close with `Escape`, reopen from another badge (or the context menu): same size as left;
  - maximize then restore the window to a small height (about 600 px), open the popup from a row near the bottom of the explorer: it opens above or shrinks, never cut by the window edge;
  - restart the app: the default size again (in memory only, by design).
  - Captures in dark and light (`SQUILL_LANGUAGE` French and English for one of them at least): (1) popup at default size, (2) enlarged popup, (3) popup opened near the window bottom. Read every PNG; check the grip is visible on both themes. Captures are not committed; report what differs in the Done notes.
- [x] Close the app, any container (`podman ps -a` empty), `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance with `Squill.Windows.slnx` (the Presentation type is built by both solutions; `Squill.slnx` must stay green too), the WinUI AOT publish (`eng/check-aot-warnings.ps1 -Head WinUI`, baseline warnings only), the app starts, captures read (dark and light), everything closed. Steps that cannot be driven are written `not driven (<why>)` in the Done notes, never claimed.
- The popup has a visible bottom-right grip, resizes between 320 x 220 and the window, the list fills the popup, the size is reused on reopen during the session, and the popup is never clipped by the window bottom.

## Risks
- Pointer press on the grip may be read by the flyout as a click outside or start a text selection: mark the press handled and capture the pointer; if the flyout still closes on press, report it.
- `TransformToVisual(null)` gives window coordinates only while the flyout is constrained to the window (today's behavior, P20-27 notes: clipped by the window edge). Do not set `ShouldConstrainToRootBounds = false`; if the limits come out wrong, stop and report.
- Rebuilding explorer nodes on a toggle re-anchors the flyout (P20-27 notes); a re-anchor must not reset the size the user dragged (the content keeps its `Width` / `Height`); check after a toggle.
- Two drives in one session already strained P20-27: drive only WinUI here.

## Suggested commit message
Make the WinUI explorer filter popup resizable and keep it inside the window

## Done notes
- Built as designed: `ExplorerFilterPopupSize` (Presentation, 8 tests), `ResizeGrip`, four-row layout, `OpensAbove` / `Fit` placement, presenter `MaxWidth` / `MaxHeight` infinite, key `explorer.filter.resize`. `Squill.Windows.slnx` and `Squill.slnx` 0 warnings, 4429 tests green, WinUI AOT check baseline only.
- Deviation: the transparent hit-test brush must be on the grip's inner `Canvas` (on the `UserControl` alone the press never reached the grip).
- Driven on the published WinUI exe (SQLite `-Scenario Full`, 150 % display): grip visible in dark/English and light/French; drag +200 x +100 grows the popup and the list; drag beyond the window stops about 8 px inside; drag up-left stops at the 320 x 220 minimum (checked by UI Automation rectangles); size reused when reopened from another badge; popup opened near the window bottom opens above (dark capture) and the grip hit-test worked there. The flyout did not close on grip press and `TransformToVisual(null)` gave window coordinates: no risk triggered.
- Difference: when an enlarged above-placed popup is taller than the room above, WinUI re-places it (light/French capture: it moved below the anchor and stayed inside the window).
- Not driven: restart check (in-memory by design), toggle re-anchor while enlarged, a maximize/restore pass; the mouse had to be injected with absolute moves (`SetCursorPos` moves produced no pointer events).
