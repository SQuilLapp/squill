# P11-32 - Result export verified per engine

Status: done
Depends on: P11-25

## Goal
CSV and JSON export (P4-11) give correct, re-importable output for the values each engine returns, including large results.

## Design decisions (final)
- Presentation tests (`Squill.Presentation.Tests`) over the all-types rows captured per engine as JSON resources (same capture mechanism as P11-28/P11-31: a Docker-gated test in each driver project writes the resource when `SQUILL_UPDATE_SNAPSHOTS=1`; SQLite resource generated normally). For each type of each engine the CSV cell and the JSON value are asserted against a canonical expected table: strings with commas, quotes, CR/LF and Unicode (emoji, accents) quoted per RFC 4180; NULL vs empty string distinct in JSON (`null` vs `""`) and in CSV (empty field both: document, CSV has no NULL; add an option `NullText` default empty if the existing exporter has none, only if cheap); `decimal` unrounded; dates ISO-8601; `byte[]` as `0x` hex in CSV and base64 in JSON (document the choice already made by the exporters and test it); arrays and JSON columns as JSON values (not strings) in JSON export; NaN/Infinity as strings in JSON.
- Round trip: the CSV of each engine imported through `Microsoft.VisualBasic`-free parsing in the test (small RFC 4180 reader in the test) equals the source rows; the JSON parses with `System.Text.Json` and has the right element kinds.
- Large export: 500,000 generated rows through `StreamingGridDataSource` to a file, constant memory (assert peak working set growth under 200 MB with `GC.GetTotalMemory` before/after and no `ToList` of rows: look at the exporter code), cancellation mid-export leaves no partial file (temp file + move; fix if not).
- Export of an edited, uncommitted grid exports the committed values or refuses: check what happens today, make it explicit (message through the catalog) and test it.

## Out of scope
- New formats (Excel, SQL INSERT), clipboard formats.

## Tasks
- [x] Resources, tests, exporter fixes.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Verify CSV and JSON export on the values of every engine
