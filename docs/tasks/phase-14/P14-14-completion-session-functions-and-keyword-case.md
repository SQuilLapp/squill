# P14-14 - Sql and Presentation: functions in the popup and the keyword-case setting

Status: done
Depends on: P14-07, P14-13

## Goal
The popup of a query tab offers the built-in and stored functions (today they are in the code and the tests but not registered in the app) and writes keywords in the case the user chose in the settings (`FormatterKeywordCase`: Upper, Lower, Preserve).

## Design decisions (final)
- **Functions**: `QueryTabViewModel.SessionCompletionProvider` merges `KeywordCompletionProvider`, `SchemaCompletionProvider` **and** `FunctionCompletionProvider` (the composite `DialectIntelligenceTests` already builds). Check by test that a SELECT list proposes `count` and a stored function of the snapshot, and that a table position does not.
- **Keyword case**: `SqlRequest` gets a trailing optional parameter `KeywordCase KeywordCase = KeywordCase.Upper` (public record; existing constructions and `with` keep compiling). A new `internal static class KeywordCasing` in `src/Squill.Sql/Completion/` has `string Apply(string canonicalUpper, SqlRequest request, int prefixStart, int prefixLength)`: `Upper` -> as is, `Lower` -> lower invariant, `Preserve` -> lower when the keyword tokens of `request.Text` are mostly lower case (strictly more lower-case than upper-case tokens of kind keyword), or, when the text has none, when the typed prefix has letters and all are lower case; otherwise upper. Multi-word keywords (`LEFT JOIN`, `GROUP BY`) are cased as a whole. The decision is computed once per request, not per item.
- `KeywordCompletionProvider` applies it to `Label`, `InsertText` and sets `FilterText` to the upper-case canonical form (matching is case-insensitive anyway). `JoinConditionText.Build` (P14-07) writes `AND` through `KeywordCasing`, and the relation row (P14-08) writes ` ON ` the same way; the signature of `JoinConditionText.Build` gains a trailing `bool lowerKeywords = false` (the resolved decision of `KeywordCasing`; add a `bool UseLowerKeywords(SqlRequest request, int prefixStart, int prefixLength)` to `KeywordCasing` for it), P14-07's tests stay valid through the default.
- **Presentation**: `QueryTabViewModel` keeps `_keywordCase` (default `Upper`), refreshed from `_settings.LoadAsync` at construction (fire and forget, errors keep the default) and again whenever a completion request finds the value older than 5 seconds (fire and forget, never blocking: the next popup sees the new value). `SessionCompletionProvider` sets `request with { Schema = session.Schema, KeywordCase = <current> }`. Known limit, written in the ADR: a settings change reaches the popup within seconds, not instantly (`ISettingsStore` has no change event).
- The formatter keeps its own use of the same setting; nothing there changes.

## Out of scope
- A separate completion-only setting, identifier case, a settings UI change.

## Tasks
- [x] Tests first: Sql (`tests/Squill.Sql.Tests/Completion/KeywordCasingTests.cs`): Upper, Lower, Preserve with an upper script, a lower script, a mixed script (majority), an empty script with typed `sel` / `SEL`; multi-word keyword; `AND` / `ON` in generated join text follow the case; default request (no `KeywordCase`) behaves as before. Presentation (`tests/Squill.Presentation.Tests`, fake `ISettingsStore`): functions present, keyword case taken from the store, refresh after the staleness window (inject a clock or a `TimeProvider`, already used elsewhere in the project: check), error in the store keeps `Upper`.
- [x] Implement, XML docs. Update `KeywordCompletionTests` only if an expectation depended on the old constructor.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Offer functions in the popup and follow the keyword case setting
