# P17-02 - Presentation: grid sort and column value counts

Status: todo
Depends on: P17-01

## Goal
The two other pure pieces of the M05 column menu: a typed row comparison for sorting and the distinct-values-with-counts list of a column. Recipe decisions 5 and 7.

## Design decisions (final)
- `src/Squill.Presentation/Results/GridSort.cs`: `public sealed record GridSort(int Column, bool Descending)` and `public static class GridSortComparer` with `Create(ColumnInfo column, bool descending)` returning an `IComparer<object?>`: NULL first ascending and last descending, typed comparison by the column category of P17-01 (numbers by value across CLR numeric types, dates, booleans false < true, text with `StringComparer.CurrentCultureIgnoreCase`, byte arrays by length then bytes, anything else by its formatted text); never throws on mixed or unexpected CLR types (falls back to formatted text).
- `GridSort.Next(GridSort? current, int column)`: the click cycle of recipe decision 5 (none -> ascending -> descending -> none, a different column starts ascending).
- `src/Squill.Presentation/Results/ColumnValueCounts.cs`: `public sealed record ValueCount(object? Value, int Count)` and `public static ColumnValueCounts` with `Compute(IGridDataSource source, int column, int cap, CancellationToken ct)` returning `(IReadOnlyList<ValueCount> Values, bool Truncated)`: distinct by the typed value (text case-sensitive here: two spellings are two values, because the `In` filter compares typed values), NULL as its own entry with `Value = null`, ordered by count descending then by the sort comparer, at most `cap` entries (`Truncated` when more existed), reads the source row by row on the calling thread and checks `ct` every 4,096 rows. It reads only `RowCount` rows that existed when it started.
- Memory: a dictionary keyed by the value, no per-row allocation beyond the boxed values the source already holds.

## Out of scope
- The view that applies the sort (P17-03), the menu (P17-05, heads).

## Tasks
- [ ] `GridSort`, `GridSortComparer`, `ColumnValueCounts`.
- [ ] Tests `tests/Squill.Presentation.Tests/Results/GridSortTests.cs`, `ColumnValueCountsTests.cs`: ascending and descending per category with NULLs, stability is not the comparer's job (a test on equal keys returns 0), the click cycle, mixed CLR types do not throw, counts and ordering, the cap and `Truncated`, cancellation stops the scan, 100,000 rows counted in well under a second (assert a generous bound, not a benchmark).

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched.

## Suggested commit message
Add the grid sort comparer and the column value counts
