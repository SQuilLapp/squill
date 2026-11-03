# P11-13 - WinUI head: result tabs for several sets and a Messages tab

Status: todo
Depends on: P11-12

## Goal
The WinUI results panel switches between the result sets of a run and shows the messages as text.

## Design decisions (final)
- Look at how the results panel binds `QueryTabViewModel.Results` today; reuse the existing result tab strip, add a plain read-only text view for `ResultKind.Messages` (monospace font token, selectable, copyable). Style from `visual-spec.md`; no new tokens.
- Drive it: run on a SQLite file `select 1; select 2;` (two statements) and check the strip; the multi-set and message cases need SQL Server and PostgreSQL containers (P11-01), run `exec` of a procedure with two selects and a `print`, and `do $$ begin raise notice 'hi'; end $$`, and capture.

## Out of scope
- Avalonia (P11-14).

## Tasks
- [ ] XAML and code-behind, captures `en`.

## Acceptance criteria
- `rtk dotnet build Squill.Windows.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.Windows.slnx -c Release` green; WinUI AOT publish with zero IL2xxx/IL3xxx warnings.
- To check by hand: run the SQL Server procedure with two selects and a print; the two sets and the Messages tab are reachable and the grid of each is editable only when it should be.

## Suggested commit message
Show several result sets and messages in the WinUI results panel
