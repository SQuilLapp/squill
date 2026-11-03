# P12-07 - Presentation: editor variable contract, popup content and quick actions

Status: todo
Depends on: P12-04, P12-02

## Goal
Everything the two heads need to colour, explain and edit variables, as UI-framework-free logic, so each head only draws. Specification: [variables-spec.md](variables-spec.md) section 1.

## Design decisions (final)
- `ISqlEditor` gets (read it and its implementers `StubSqlEditor`, `PlainSqlEditor`, `LoomSqlEditor`, `AvaloniaSqlEditor` first; stubs get empty bodies):
  - `void SetVariables(IReadOnlyList<VariableMark> marks)` where `VariableMark(TextRange Range, string Name, VariableVisualState State)` and `VariableVisualState` is `Resolved`, `Unresolved`, `Secret`;
  - `event EventHandler<VariableActivatedEventArgs>? VariableActivated` (name, range, `VariableActivation` = `Click` or `Keyboard`, plus an optional screen anchor `(double X, double Y)?`), raised by the head when the user clicks or presses `Ctrl+.` on a variable.
- `VariableEditorController` (Presentation): listens to `ISqlEditor.TextChanged` (debounced by the head's existing timer, not here), `IVariableService.Changed` and dialect changes; runs `VariableScanner.Scan` on the editor text, resolves every name and calls `SetVariables`. Cheap for 100,000-line scripts: scans only the statement window it is told (`SetVisibleRange(int firstOffset, int lastOffset)`, called by the head on scroll; default whole text under 200 KB).
- `VariablePopupModel` built from a `ResolvedVariable`: title (`{{name}}`), state, value text (masked as `********` for a secret; a long value is truncated at 200 chars with an ellipsis), scope label, shadowed scope labels, hint text, and the list of available actions. Used for the hover Markdown (LoomEditor) and for the Avalonia popup, so both heads say the same thing. Strings through `UiText` (`variables.popup.*`).
- `VariableQuickActions` (view-model): actions `EditValue`, `CreateIn(scope)` for Global / Environment (disabled with reason when the connection has no environment) / Connection, `ChangeScope`, `SetForTab`, `ToggleSecret`, `ToggleAllowRaw`, `OpenManager`, `Delete`; each executes through `IVariableService`; editing a value needs a small input, requested through a new `IDialogService.PromptVariableAsync(VariableDraft draft)` (name, scope choices, value, secret, allow raw; returns the edited draft or null) implemented by each head in P12-09 and P12-11.
- `VariableChipsViewModel`: the distinct variables of the active statement (`SqlStatementSplitter` at the caret, or the selection) as `VariableChip(name, display text "{{name}} = value" masked when secret, state)`; updates on caret, text and service changes; `Activate(chip)` raises the same quick-action flow.
- Colours are not here; heads read design tokens `variable.resolved`, `variable.unresolved`, `variable.secret` added by P12-08 and P12-11.

## Out of scope
- Any drawing, WinUI or Avalonia code.

## Tasks
- [ ] Interface additions and stubs, controller, popup model, quick actions, chips view-model, dialog contract, catalog keys fr/en (`variables.popup.*`, `variables.action.*`, `variables.chip.*`).
- [ ] Tests with `StubSqlEditor`: marks for resolved / unresolved / secret, update after `Changed`, masking, truncation, actions available per state and per environment, each action changes the service as expected, chips for the statement at the caret and for a selection, a script of 100,000 lines does not scan outside the visible window.

## Acceptance criteria
- Build 0 warnings (both heads still compile with the new interface members); tests green; parity test for the catalogs green.

## Suggested commit message
Add the editor variable contract, popup model and quick actions
