# P20-30 - Avalonia: a resizable explorer filter popup, its size kept for the session

Status: done
Depends on: P20-28, P20-29

## Goal
Bring P20-29 to the Avalonia head: a bottom-right resize grip on the explorer filter popup, sizes between `ExplorerFilterPopupSize.Minimum` and the window, the size kept for the session and reused on reopen, the popup kept inside the main window, and the row list growing with the popup. The size rules (`src/Squill.Presentation/Explorer/ExplorerFilterPopupSize.cs`: `Default`, `Minimum`, `WindowMargin`, `Fit`, `OpensAbove`) already exist from P20-29; this card only draws them.

Files today: `src/Squill.App.Avalonia/Views/ExplorerFilterMenu.cs` (`Flyout` placement `BottomEdgeAlignedLeft`; content a `StackPanel` of width 440: tool bar, search box, `ScrollViewer` with `MaxHeight` 420 around the `FilterRows` panel; `Show` returns the flyout for the tests), `Views/ObjectExplorerView.axaml.cs` (`OnBadgeClicked` anchors to the badge; `OnChooseShownClicked` anchors to the whole `Tree` because the explorer rebuilds row nodes on every toggle, P20-28 Done notes), `Styles/Controls.axaml` (`FlyoutPresenter` style, radius only), the pointer precedent `Controls/DataGridCanvas.cs` (`Cursor = new Cursor(StandardCursorType.SizeWestEast)`), and the headless tests `tests/Squill.App.Avalonia.Tests/ExplorerFilterPopupUiTests.cs`.

## Design decisions (final)
- **The popup stays a `Flyout`**: its size follows the content. Lift the Fluent presenter limits with a class: `flyout.FlyoutPresenterClasses.Add("explorer-filter")` and in `Styles/Controls.axaml` `FlyoutPresenter.explorer-filter`: `Padding 0`, `MaxWidth Infinity`, `MaxHeight Infinity`. If the presenter still scrolls or clips the content, stop and report rather than switching to a `Popup`.
- **Remembered size**: `internal static ExplorerFilterPopupSize? Remembered { get; set; }` on `ExplorerFilterMenu` (XML doc; internal so the headless tests can reset it), set at the end of each drag, used by `Show` as `Remembered ?? ExplorerFilterPopupSize.Default`. In memory only, as in P20-29 (persistence decision and how to add it later: P20-29).
- **Layout** (`Build`): the content becomes a `Grid` (`RowDefinitions="Auto,Auto,*,Auto"`, the 6 px gaps of today's `Spacing` kept as top margins) with `Width` / `Height` from the fitted size: tool bar, search box, `ScrollViewer` (no `MaxHeight`, fills the `*` row), grip row. Remove `ListHeight`; the width comes from `ExplorerFilterPopupSize`.
- **Grip**: new `src/Squill.App.Avalonia/Views/ResizeGrip.cs`, `internal sealed class ResizeGrip : Canvas` (one-line `<summary>`), two short diagonal `Line`s as children (the classic corner grip), stroke `SquillFg3Brush` through `DynamicResource`/`GetResourceObservable`, like `IconButton` binds its icon), `Name = "ResizeGrip"`, 14 x 14, right-aligned, margin `0,0,2,2`, `Cursor = new Cursor(StandardCursorType.BottomRightCorner)`, `AutomationProperties.Name` = `explorer.filter.resize` (key added by P20-29).
  - The resize math lives in `internal void ResizeBy(Vector delta)` (start size + delta -> `Fit` -> panel `Width` / `Height`), called by the pointer handlers, so the headless tests drive it without synthetic pointer input.
  - `PointerPressed`: `e.Pointer.Capture(this)`, `e.Handled = true`, keep the start pointer position (relative to the grip's own `TopLevel`, the popup root), the start size, and the limits in screen pixels so they work whether the popup is an overlay or its own window: `main = TopLevel.GetTopLevel(anchor)`, `bottomRight = main.PointToScreen(new Point(main.ClientSize.Width, main.ClientSize.Height))`, `topLeft = panel.PointToScreen(default)`, `max = (bottomRight - topLeft) / main.RenderScaling - WindowMargin`.
  - `PointerMoved` while captured: `ResizeBy(current - start)`. `PointerReleased` / `PointerCaptureLost`: release, set `Remembered`.
  - No edge grips (same reason as P20-29).
- **Opening inside the window** (`Show(Control anchor, ExplorerFilterViewModel vm, Rect? row = null)`): `row` is the row's bounds in `anchor` coordinates when the anchor is the whole tree (context menu path), `null` means the anchor itself. In main-window coordinates (`anchor.TranslatePoint`), `spaceBelow = ClientSize.Height - rowBottom - WindowMargin`, `spaceAbove = rowTop - WindowMargin`; `above = OpensAbove(...)`; size = `Fit(wanted, ClientSize.Width - rowLeft - WindowMargin, above ? spaceAbove : spaceBelow)`; placement `TopEdgeAlignedLeft` / `BottomEdgeAlignedLeft`. With a `row`, place the flyout against the row and not the tree with `VerticalOffset` (`row.Bottom - anchor.Bounds.Height` for bottom placement, `row.Top` for top placement) and `HorizontalOffset = row.Left`. `OnChooseShownClicked` passes the bounds of the context menu's `PlacementTarget` (the row) translated into `Tree` coordinates, measured when the item is clicked. If `PopupFlyoutBase.VerticalOffset` / `HorizontalOffset` do not exist in the Avalonia version in `Directory.Packages.props`, use `PlacementMode.Pointer` for that path instead and say so in the Done notes.
- No `ItemsSource`, brushes and icons through resources as today.

## Out of scope
- WinUI (P20-29), saving the size in settings, edge grips, following a window resize while the popup is open, view-model changes.

## Tasks
- [x] Tests first, headless, in `tests/Squill.App.Avalonia.Tests/ExplorerFilterPopupUiTests.cs` (same fixture; each test sets `ExplorerFilterMenu.Remembered = null` first; the `Headless` collection already runs them one at a time):
  - `Popup_opens_at_the_default_size_with_a_grip`: content `Width` 440 and `Height` 520 (or the fitted size if the test window is smaller: compare with `ExplorerFilterPopupSize.Fit`), a visible control named `ResizeGrip`.
  - `Resizing_grows_the_popup_and_the_list`: `ResizeBy(new Vector(100, 80))`, run the layout (`Dispatcher.UIThread.RunJobs()`): content 540 x 600 (or clamped), the `ScrollViewer` around `FilterRows` is 80 px taller than before.
  - `Resizing_stays_between_the_minimum_and_the_window`: `ResizeBy(-10000, -10000)` gives 320 x 220; `ResizeBy(10000, 10000)` gives a content no larger than the window client size.
  - `Reopening_reuses_the_last_size`: resize, release (or set the size through the release path), hide, `Show` again: same `Width` / `Height`.
  - See them fail, then implement. Existing popup tests stay green.
- [x] Implement the presenter class, the grip, the layout, the placement and the context-menu row bounds.
- [x] Publish the Avalonia AOT head and drive it with `Drive-Window.ps1` as in P20-29 (SQLite `-Scenario Full`): default size, drag the grip larger and smaller, reopen at the same size, open near the window bottom of a short window; dark and light, French and English. Avalonia popups were invisible to the capture tools in P20-19 and P20-28: if the flyout is missing from the capture, write `not driven (popup not captured)` in the Done notes and list what the owner should look at (grip visible on both themes, cursor shape, list growth, no clipping at the window bottom, context-menu popup next to the row); never claim it.
- [x] Close the app, `dotnet build-server shutdown`.

## Acceptance criteria
- Recipe 2 common acceptance of [verification-recipe.md](verification-recipe.md) with `Squill.slnx`, the Avalonia AOT publish (`eng/check-aot-warnings.ps1`), the app starts; the four headless tests green; captures read or recorded as not driven; everything closed.

## Risks
- In a windowed Avalonia popup, coordinates relative to the main window and to the popup differ: keep the limits in screen pixels (above) and the drag delta in the grip's own top level.
- Pressing the grip must not light-dismiss the flyout or start a drag of the tree; mark the press handled.
- The tree-anchored context-menu path (P20-28) placed the popup under the whole tree; the row offset fixes that, check it in the drive or list it for the owner.

## Suggested commit message
Make the Avalonia explorer filter popup resizable and keep it inside the window

## Done notes
- Built as designed: `ResizeGrip` (Canvas, two lines, `BottomRightCorner` cursor, `ResizeBy` / `EndResize`), `ExplorerFilterMenu.Remembered`, four-row `Grid` layout (no `MaxHeight`), `FlyoutPresenter.explorer-filter` style, `Show(anchor, vm, row)` with `OpensAbove` / `Fit`, and `OnChooseShownClicked` passing the row bounds (`ContextMenu.PlacementTarget` translated into tree coordinates). `PopupFlyoutBase.HorizontalOffset` / `VerticalOffset` exist in Avalonia 12.1.6, so no `PlacementMode.Pointer` fallback.
- Four headless tests added (176 Avalonia tests green, 4650 solution-wide with 342 skipped as before). `Squill.slnx` and `Squill.Windows.slnx` 0 warnings; Avalonia AOT check (`eng/check-aot-warnings.ps1 -Head Avalonia`) baseline only; the published exe starts and stays up.
- Deviations: the drag works in screen pixels (pointer and limits) so it also holds when an above-placed popup's origin moves while it grows; the headless flyout never attaches its content, so the tests lay it out by hand and `ResizeGrip` falls back to the anchor's corner for the limit when the panel has no top level. The tests open the popup against a small top row rect, because anchored to the whole view there is no room below in the test window.
- `not driven (popup not captured)`: the Avalonia popup was not driven on the published exe. For the owner to look at: grip visible on both themes, cursor shape, list growth, no clipping at the window bottom, popup next to the row from the context menu.
