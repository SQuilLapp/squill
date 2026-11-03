# P10-08 - WinUI head: catalog markup for tooltips, headers, titles and menus

Status: done
Depends on: P10-02

## Goal
`Loc.Key` only fills `TextBlock.Text`, `TextBox.PlaceholderText` and `ContentControl.Content`. Add the other targets so that WinUI XAML can reference the catalog everywhere a literal appears today. Avalonia needs nothing: `{loc:Text key}` already works on any string property.

## Design decisions (final)
- In `src/Squill.App.WinUI/Localization/Loc.cs` add exactly these attached properties, each a plain `DependencyProperty` (no reflection, AOT-safe) whose change callback sets the target from `UiText.Get(key)`:
  - `Loc.ToolTipKey` -> `ToolTipService.SetToolTip(element, text)` on any `DependencyObject`.
  - `Loc.HeaderKey` -> `Header` of `ComboBox`, `TextBox`, `NumberBox`, `ToggleSwitch`, `Expander`, `TabViewItem`, `DataGridColumn`-like controls the app uses (the callback switches on the concrete types present in the head; unknown type: do nothing).
  - `Loc.TitleKey` -> `MenuBarItem.Title` and `ContentDialog.Title`.
  - `Loc.MenuTextKey` -> `MenuFlyoutItem.Text` and `ToggleMenuFlyoutItem.Text`.
  - `Loc.AutomationNameKey` -> `AutomationProperties.SetName`.
- Existing `Loc.Key` behavior is unchanged. Document every property with the types it supports. A migration card that needs another target adds it in its own commit, in the same style.
- Resolution happens once when the property is set at XAML load (same as `Loc.Key`): consistent with the restart-only decision of P10-04.
- Use each new property in one real place already in the inventory as the smoke test (one menu item, one tooltip), chosen by the implementer; the other uses belong to the area cards.

## Out of scope
- Migrating strings in bulk, Avalonia.

## Tasks
- [x] Add the five properties with XML doc.
- [x] Smoke use in two places; verify by launching the exe with `SQUILL_LANGUAGE=en` and hovering / opening the menu (capture or "to check by hand" in the report).

## Acceptance criteria
- `rtk bypass dotnet build Squill.Windows.slnx -c Release -warnaserror` exits 0 with 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` is green.
- `rtk dotnet publish src/Squill.App.WinUI -c Release -r win-x64` succeeds with zero IL2xxx/IL3xxx warnings.
- No process left running.

## Suggested commit message
WinUI: catalog markup for tooltips, headers, titles and menu items
