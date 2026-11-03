# P12-05 - Presentation: substitution at run time

Status: todo
Depends on: P12-02, P12-04

## Goal
Running a statement that contains `{{name}}` resolves and substitutes it safely, and fits with the existing parameter prompt. Specification: [variables-spec.md](variables-spec.md) section 5.

## Design decisions (final)
- `VariableSubstitutor.Prepare(string statement, DatabaseDialect dialect, IVariableService variables)` in `Squill.Presentation.Variables` returns `SubstitutionPlan`: the statement text with variables replaced, the extra `QueryParameter`s (`sqlvar1`, `sqlvar2`, ... by first appearance, same name = same parameter), the list of unresolved names, the list of `raw` variables used and the list of problems (raw not allowed, raw on a secret, invalid identifier value). Pure and synchronous over the service snapshot.
- Replacement rules exactly as spec section 5: `Bound` -> `@sqlvarN`; `Identifier` -> each dot-separated part quoted with `SqlQuoting` of the dialect (value empty, containing a control character or a quote-breaking sequence handled by the quoting routine); `Raw` -> text, only when the variable has `AllowRaw` and is not secret. Values are never rescanned.
- `QueryTabViewModel` (`PromptValuesAsync` / `RunStatementAsync`, read them first): before `ParameterScanner`, call `Prepare` per statement. Unresolved names are added to the names passed to `IDialogService.PromptParametersAsync` (new optional argument marking which names are variables so the heads can label them; default keeps the old signature working); entered values go to the Tab scope through the service (`UpdateAsync(Tab)`), so the next run and the hover popup see them. Cancelling the prompt cancels the run as today. A `Raw` use asks `IDialogService.ConfirmAsync` (existing confirm service, or add one method if none exists: read `IDialogService`) once per tab session per variable, showing the variable and the final text.
- Problems become a message result with the `variables.*` keys, no statement is sent.
- History (`IQueryHistoryStore`) receives the **original text** (`{{name}}` kept), the status bar and result messages never contain a resolved value; the error text of a driver that echoes the statement is shown as the driver returns it (document this limit in the ADR: a driver message can echo a bound value on some engines).
- `ParameterScanner` is untouched; its `@name` parameters coexist with `@sqlvarN` (a user `@sqlvar1` is renamed by the substitutor: pick the first free `sqlvarN`).
- The positional check (`?`, `$1`) runs on the substituted text as today.

## Out of scope
- Editor, UI, real engines (P12-06).

## Tasks
- [ ] `VariableSubstitutor`, `SubstitutionPlan`, integration in the query tab, dialog contract extension, catalog keys fr/en.
- [ ] Tests with a fake connection that records the SQL and parameters: bound, repeated name, two names, unresolved -> prompt -> value used and stored in the Tab scope, cancel, `ident` with `schema.table`, `ident` with a hostile value (`a"; drop table x; --`), `raw` allowed / not allowed / secret / confirmation, variable inside a string not replaced, user `@sqlvar1` collision, value containing `{{other}}` not rescanned, history entry keeps the original text, secret value absent from status and results.

## Acceptance criteria
- Build 0 warnings; tests green.

## Suggested commit message
Substitute query variables as bound parameters when a statement runs
