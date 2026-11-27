# P21-60 - Presentation: chart model from a result set

Status: todo
Depends on: none

## Goal
Turn a result into a simple chart (bar, line, area, scatter, pie) with a category or numeric/time X axis and one or more numeric series, like DBeaver's and DataGrip's result charts.

Exists today: nothing for charts; the result rows are in `IGridDataSource` (streaming, max grid rows), `ColumnValueCounts` counts values per column.

## Design decisions (final)
- `ChartViewModel` (Presentation/Results/Charts): kind, X column, Y columns, aggregation when X repeats (`None`, `Sum`, `Count`, `Avg`, `Min`, `Max`), sort by X, a default guess (first text/date column as X, numeric columns as Y, `Bar` for text X, `Line` for date/number X).
- Computes `ChartData` (series, points, axis ranges, "nice" tick values with a pure `AxisTicks`, date ticks per day/month/year) off the UI thread over the **loaded** rows; cap 10,000 points per series (aggregated or sampled with a notice); pie limited to 12 slices plus `Other`.
- Export the chart as SVG (pure writer, like `ErSvgExporter`) and the chart data as CSV.
- Settings per result tab in memory only.

## Out of scope
- Heads (P21-61, P21-62), dashboards, live charts on auto-refresh (it simply recomputes when the result is replaced).

## Tasks
- [ ] Tests: default guess, aggregation, ticks (numbers, dates), caps, SVG golden output.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- None specific.

## Open points (defaults)
- No chart library (drawn by the heads like the plan graph); the owner may prefer a package (`LiveCharts2`, `ScottPlot`) but AOT and WinUI support would need a probe first.

## Suggested commit message
Build chart data from a result set
