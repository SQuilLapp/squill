# P21-63 - Presentation: column statistics (count, nulls, distinct, min, max, mean, histogram)

Status: todo
Depends on: none

## Goal
Statistics of a result column, like DataGrip's column statistics: rows, nulls, distinct values, min, max, mean / median for numbers, length range for text, and a histogram.

Exists today: `ColumnValueCounts` (distinct values with counts, used by the column menu value filter, P17-02) over the loaded rows; `ColumnMenuViewModel`.

## Design decisions (final)
- `ColumnStatistics` (pure, Presentation/Results) computed from the loaded rows (the grid's `IGridDataSource`, or the filtered view when a filter is active - said in the header), reusing `ColumnValueCounts` for distinct and top values: count, nulls (and %), distinct, top 10 values, min/max (numbers, dates, text ordinal), mean, median and standard deviation for numbers, min/avg/max length for text, a 20-bucket histogram for numbers and dates (equal width; dates by natural unit).
- Off the UI thread, cancellable when the result changes; `ColumnMenuViewModel.Statistics` lazily computed when the user opens the `Statistics` section.
- The header says "over N loaded rows" (never claims the whole table); a `Compute on server` command runs one aggregate query (`COUNT`, `COUNT(DISTINCT)`, `MIN`, `MAX`, `AVG`) on the source table when it is known (`EditableSourceAnalyzer`), read-only, through the normal run path.

## Out of scope
- Heads (P21-64).

## Tests and tasks
- [ ] Tests: each statistic per type, nulls, histogram buckets, filtered view, server query text per dialect.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- Median on 1,000,000 rows: use a selection algorithm, not a full sort, or cap at 1,000,000 with a notice.

## Open points (defaults)
- None.

## Suggested commit message
Compute column statistics over the loaded rows
