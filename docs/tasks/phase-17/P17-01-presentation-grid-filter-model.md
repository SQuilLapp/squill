# P17-01 - Presentation: grid filter model and matching

Status: done
Depends on: none

## Goal
A pure model of one grid filter (column, operator, operand) with the matching rule on a cell value and the display text of the chip. Read [filter-palette-recipe.md](filter-palette-recipe.md) first (decisions 1, 4, 6).

## Design decisions (final)
- New files in `src/Squill.Presentation/Results/`: `GridFilterOperator` (the 12 operators of recipe decision 4), `GridFilter` (`sealed record GridFilter(int Column, GridFilterOperator Operator, string? Text, IReadOnlyList<object?>? Values)`: `Text` is the typed operand of the single-value operators, `Values` the set of `In`; `Values` may contain `null` for the NULL row), `GridFilterMatcher`, `GridFilterText`.
- `GridFilterMatcher.Operators(ColumnInfo column)` returns the operators offered for the column's category (text: all but the four comparisons; number, date/time: `Equals`, `NotEquals`, the four comparisons, `IsNull`, `IsNotNull`, `In`; boolean: `Equals`, `NotEquals`, `IsNull`, `IsNotNull`; other: `IsNull`, `IsNotNull`, `Contains`). The category comes from the existing helpers (`CellEditorKinds.For(column)` in `Editing/`, `DisplayFormat`, `CellFormatter`): reuse them, add no second type table; if a category cannot be derived from them, add one `static` method next to `CellEditorKinds` and say so in the commit message.
- `GridFilterMatcher.TryCreate(ColumnInfo column, GridFilterOperator op, string? text, out GridFilter filter, out string? error)` validates the operand (required except `IsNull` / `IsNotNull`; parsed with `CellValueParser.TryParse` for typed categories; the error is the parser's message) and returns the filter with the operand normalized.
- `GridFilterMatcher.Matches(GridFilter filter, ColumnInfo column, object? cell)` implements recipe decision 4 exactly (typed compare for number/date/boolean, formatted-text compare for the text operators, NULL rule, `In` against typed values with `null` allowed). Parse the operand once: add an `internal` compiled form (`Compile(filter, column)` returning a `Func<object?, bool>`-like sealed class, no delegates allocated per row) so P17-03 can test 100,000 rows cheaply.
- `GridFilterText.Format(GridFilter filter, ColumnInfo column)` builds the chip text of recipe decision 6 (`ville IN ('Lyon', 'Paris', +1)`, `montant > 100`, `siret IS NULL`, strings quoted with `'` doubled, numbers and booleans bare, dates quoted in the invariant round-trip format of the cell formatter); operator symbols are language-neutral so no `UiText` is involved here.

## Out of scope
- Any view, sort or UI (P17-02 ...), the `OR` of filters, regular expressions, localizing the operator names for the filter editor (P17-05 owns their `UiText` keys).

## Tasks
- [x] `GridFilterOperator`, `GridFilter`, `GridFilterMatcher` (operators per column, `TryCreate`, compiled matching), `GridFilterText`.
- [x] Tests `tests/Squill.Presentation.Tests/Results/GridFilterMatcherTests.cs` and `GridFilterTextTests.cs`: every operator on text, integer, decimal, date-time, boolean and byte-array columns; NULL cells (only `IsNull` / `In` with null match; `NotEquals` does not); case-insensitive text; accented text; an invalid typed operand returns an error and no filter; `In` with a null entry; chip text with 1, 3 and 5 values and a quote inside a string.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.

## Suggested commit message
Add the grid filter model, matching and chip text
