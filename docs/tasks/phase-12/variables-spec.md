# Phase 12 - Query variables: shared specification

Final decisions for every card of phase 12. Cards refer to a section instead of repeating it. ADR 0009 (P12-15) records them.

## 1. What the user sees (Postman / Bruno principle)

- The text itself holds the variables: `SELECT * FROM orders WHERE customer_id = {{customerId}}`.
- Every `{{name}}` in the SQL editor is coloured by state: **resolved** (green), **unresolved** (orange/red, same palette as the diagnostics), **secret** (resolved, purple, value masked). Colours are tokens of the design system (P9-01), light and dark.
- Hovering a variable opens a popup: name, current value (masked for a secret), the scope it comes from, the scopes that define it but are shadowed, and for an unresolved one "Not defined in any scope" with the create actions.
- Clicking a variable (WinUI: see the limits in section 7) opens the quick actions: **Edit value**, **Create in ... / Change scope** with one entry per scope (Global, Environment, Connection), **Set for this tab only**, **Make secret / not secret**, **Open variables manager**.
- A chip row under the editor (Bruno): one chip per distinct variable of the active statement or selection, `{{name}} = value`, same colours, click opens the same quick actions. It is also the fallback where a head cannot click inside the text.

## 2. Syntax

- Reference: `{{name}}`, optional spaces inside (`{{ name }}`). Name: `[A-Za-z_][A-Za-z0-9_.-]*`, at most 64 chars, case-sensitive. Anything else between the braces is not a variable and stays plain text (and the SQL diagnostics report it as before).
- Why it is safe in SQL: `{{` is not valid SQL in any of the four dialects outside a string, a comment or a quoted identifier (ODBC escapes are a single `{fn ...}`; T-SQL `{` is not an operator). It does not touch `::` casts, `$1`, `$$` bodies, `@p`, `:p` or `?`.
- Recognised only **outside** string literals, comments, quoted identifiers and dollar-quoted bodies. A `{{x}}` inside `'...'` is text, never substituted, never coloured (this is the main safety rule: no accidental injection into a literal; the user writes `= {{x}}` without quotes and gets a bound parameter, section 5).
- Modifiers (prefix before the name, inside the braces):
  - none: `{{name}}` is a **bound parameter** (value position).
  - `{{ident:name}}`: the value is an **identifier**, quoted with the dialect rules (`SqlQuoting`, P7-09), may contain a dot (`schema.table` quotes each part). For table, column and schema names.
  - `{{raw:name}}`: the value is pasted **as text**. Allowed only when the variable is flagged *Allow raw* (default off) and the user confirms once per run (section 5). For SQL fragments (`ORDER BY`, `LIMIT` on engines that cannot bind it).
- The existing parameter syntax (`:name`, `@name`, `$name`) is **unchanged** and keeps meaning "ask me at run time" (query-local, prompt of P4-08). It is not coloured as a variable. `{{name}}` is for named, saved, shareable values; `:name` for one-off values.

## 3. Scopes and precedence

Highest wins (the same name in several scopes is allowed and shown as shadowed in the popup):

1. **Tab** (query-local): values entered in the parameter prompt for `{{x}}` or set with "Set for this tab only"; memory only, lost when the tab closes.
2. **Connection**: saved with a connection profile (`ConnectionProfile.Id`).
3. **Environment**: saved per `ConnectionEnvironment` kind (Production, Staging, Development, Local); applies to every connection of that kind. `None` has no scope.
4. **Global**: app-wide.

Environment sits between Connection and Global so "Production" connections share `schema = prod` while one connection can still override it. Folders do not get a scope in phase 12 (follow-up, see ADR).

## 4. Storage

- Non-secret definitions: `variables.json` in the app data directory (`AppPaths`), one JSON file, source-generated context, same atomic-write pattern as the other stores (`JsonFileStore`, P2-04). Shape: list of `{ scope, ownerId?, name, value?, isSecret, allowRaw, description? }` where `ownerId` is the profile id for Connection and the environment name for Environment, absent for Global.
- Secret variables: only the metadata goes in `variables.json` (`value` absent); the value goes in `ISecretStore` under `SecretKeys.Variable(scope, ownerId, name)` (`variable:<scope>:<ownerId|->:<name>`). Never in logs, history, status text, exception messages, exports or the verification reports.
- Deleting a profile deletes its Connection variables and their secrets (extend `IProfileStore.DeleteAsync` callers, not the interface).
- The query history keeps the **text with `{{name}}`**, never the resolved values.

## 5. Run-time substitution

Done in Presentation (`VariableSubstitutor`, P12-05) just before the existing `ParameterScanner.Rewrite` step, per statement, after `SqlStatementSplitter`:

1. Scan variables (`VariableScanner`, P12-02) of the statement and resolve them (P12-01).
2. Unresolved: the parameter prompt (P4-08) is shown with those names added (labelled as variables); the entered values become Tab-scope values. Cancelling cancels the run. No silent NULL, no silent empty string.
3. `{{name}}` -> bound parameter `@sqlvarN` (N = order of first appearance; distinct from user `@names`); value passed as `QueryParameter` text exactly like prompted parameters today. Same name twice = same parameter.
4. `{{ident:name}}` -> quoted identifier text (value validated: non-empty, no control chars, parts split on `.`, each quoted by `SqlQuoting`).
5. `{{raw:name}}` -> pasted text, only for variables with *Allow raw*; each run shows a confirmation naming the variable and the final text first time per tab session. Refused for a secret variable.
6. After substitution the statement goes through `ParameterScanner` as before (`:name` prompts, positional check).
7. A variable value is never re-scanned (no recursion, `{{a}}` whose value is `{{b}}` stays the literal text `{{b}}` as a bound value).
8. Secret values are resolved at run time only, held in memory for the call, and never copied into messages.

The bound-parameter path works unchanged on all four providers because it reuses the `@name` binding the drivers already do; `ident`/`raw` are text-only and independent of the driver.

## 6. Where variables are recognised in phase 12

- The SQL editor (both heads), the run path, snippets (their text is inserted into the editor and behaves as editor text), the chip row.
- **Not** in phase 12 (proposals in the ADR): connection profile fields (host, database, user), the table designer, the terminal, result export names. Asked to the owner as an open question.

## 7. Editor capabilities (checked against LoomEditor.WinUI 0.4.0 public API on 2026-11-03)

| Need | LoomEditor.WinUI | Decision |
|---|---|---|
| Coloured ranges by state | `DecorationsCollection` + `CustomDecorationStyles` (already used by `LoomSqlEditor` for syntax colours) | Possible. A second collection `_variables` for variables so state changes do not redraw the syntax decorations. Background tint is not offered by `CustomDecorationStyle` as far as read (Foreground only): use foreground colour + the chip row for the "pill" look. |
| Hover popup | `IHoverProvider` returning `Hover(MarkdownString[])`, shown by LoomEditor's own `HoverWidget` | Possible for the information (name, value, scope, shadowed scopes). Markdown is read-only text: no buttons inside it. |
| Actions inside the hover popup | `MarkdownString.IsTrusted` exists but no public command-link handler or content widget API was found; `OverlayHost` is public but is created by the control | **Impossible as in Postman** (a "Create variable" button inside the hover). Fallback below. |
| Click on a variable | No public pointer or `MouseTarget` event on the control was found | Fallback: `CaretChanged` fires when a click puts the caret inside a variable range: after a 250 ms debounce, if the caret is **inside** a variable range and the selection is empty, show a XAML `Flyout` (quick actions) anchored on the host grid at the caret position (`_editor` exposes coordinates of a position: if no public API does, anchor under the editor's top-left and say so in the report). Also the keyboard `Ctrl+.` and the chip row. |
| Avalonia (AvaloniaEdit) | `DocumentColorizingTransformer` (already `SqlColorizer`), `PointerMoved` + `TextView.GetPositionFromPoint` for hover, `ToolTip`, `PointerPressed` | Everything possible, including real hover popup with buttons (a `Popup`/`Flyout` kept open while the pointer is over it) and click. |

If a later LoomEditor version offers a content widget or link handler, only P12-09 changes.

## 8. Tests and verification rules

- Core/Sql/Presentation: plain unit tests, run anywhere. Substitution tests on the four engines: SQLite always, PostgreSQL/MySQL/SQL Server Docker-gated (`rtk pwsh eng/run-docker-tests.ps1 -Engine all`), never reported verified when skipped.
- Injection tests are mandatory in P12-05 and P12-06: a value such as `1; DROP TABLE orders; --` or `' OR '1'='1` used as `{{x}}` must be returned as data (zero rows or a literal match), the table must still exist afterwards.
- Secret leak tests (P12-13): no secret value in history, status text, result messages, exception text, `variables.json`, logs.
- Visual cards follow `docs/tasks/phase-11/verification-recipe.md` section 2 (capture with `SQUILL_LANGUAGE=en` and `fr`; what cannot be driven is listed "to check by hand").
