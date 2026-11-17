# P17-03 - Presentation: the filtered and sorted grid view (risky)

Status: done
Depends on: P17-01, P17-02

## Goal
`FilteredGridDataSource`, an `IGridDataSource` over another one that shows only the rows matching a list of filters and a search text, in the order of an optional sort, and follows a result that is still streaming. Recipe decisions 1, 2, 4, 5 and risks "row identity" and "large results".

## Design decisions (final)
- `src/Squill.Presentation/Results/FilteredGridDataSource.cs`, `public sealed class FilteredGridDataSource : IGridDataSource, IDisposable`. Constructor: the inner `IGridDataSource`, an `IUiDispatcher` (same use as `StreamingGridDataSource`), and a `TimeProvider`-free design (no clock needed). Members: `Columns` (inner), `RowCount` (matching rows), `IsComplete` / `IsTruncated` (inner), `GetCell(viewRow, column)` (maps through the index, `null` outside), `long SourceRow(long viewRow)` (the mapping, -1 outside), `long SourceRowCount` (inner `RowCount`, for the footer), `Changed`.
- `Apply(IReadOnlyList<GridFilter> filters, string? search, GridSort? sort)` sets the view and rebuilds. Matching uses the compiled filters of P17-01; the search is a case-insensitive `Contains` on the formatted text (`CellFormatter`) of any column, empty or whitespace = no search. With no filter, no search and no sort the class is a pass-through (`SourceRow(i) == i`, no index allocated); the caller (P17-04) normally does not even create the view then.
- **Index.** `long[]` growable (or chunked like `StreamingGridDataSource` when `RowCount` can pass 2^31; the card picks chunked, as the source is chunked) holding source row numbers. No sort: matching rows in source order, **appended incrementally** when the inner source raises `Changed` (only rows `[lastSeen, inner.RowCount)` are tested). Sort: the full index is rebuilt and sorted (stable, `GridSortComparer`) when the inner source completes and on `Apply`; while the inner source still streams, the sorted index is rebuilt at most once per second (a single in-flight rebuild, the last request wins) so the view is usable and never churns.
- **Off the UI thread.** A rebuild (`Apply`, the sorted refresh, the completion rebuild) runs on a worker (`Task.Run`) over the inner rows that existed when it started, checks a `CancellationToken` every 4,096 rows, builds a **new** index and publishes it with one atomic swap under a lock; a newer `Apply` cancels the older run and a cancelled or stale run never publishes (the rule of P16-01 `a stale result must never overwrite a newer one`, same test shape). `Changed` is raised through the dispatcher after each publish, throttled to 50 ms like the streaming source. `GetCell` and `RowCount` are always consistent with the published index (read both under the lock or from one immutable snapshot object).
- The view never reads the inner source from two places at once beyond what `IGridDataSource` allows (`GetCell` is thread safe on the streaming source); it does not add a lock around inner reads on the UI thread.
- `Dispose` cancels the running rebuild and unsubscribes from the inner `Changed`.
- No edit API: the class is read-only on purpose (recipe decision 3); `ResultViewModel` (P17-04) is what refuses edits.

## Out of scope
- Selection reset, view-model state, texts (P17-04), menus and chips (heads), exporting the view.

## Tasks
- [x] `FilteredGridDataSource` as above.
- [x] Tests `tests/Squill.Presentation.Tests/Results/FilteredGridDataSourceTests.cs` with a fake inner source whose rows can be appended and completed and an immediate dispatcher: filter, search, filter + search, sort, filter + sort; rows appended while streaming appear (unsorted) and are re-sorted after completion; `SourceRow` maps back to the right inner row for every view row; `Apply` twice quickly publishes only the second result (use a gate in the fake to hold the first rebuild, then release it: its result must be discarded); empty result; the pass-through case; `Dispose` stops work; 200,000 rows filter and sort finish within a generous bound and the calling thread returns from `Apply` immediately.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched. The tests run several times in a row without failing (`rtk bypass dotnet test` twice): they must not depend on timing, only on explicit gates.
- Stop rule: if the inner source cannot be read safely from a worker thread (a data race found), stop and report with a proposed card fix; never copy the whole source to dodge it silently.

## Suggested commit message
Add the filtered and sorted grid view over a streaming result
