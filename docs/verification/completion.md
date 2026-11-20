# Completion verification (P14-18)

Status: **automated done, agent pass partial (P19-15 Avalonia: popup after `select * from cl`; P20-12 WinUI: dark, French, popup, join row, Enter, Ctrl+Z, stuck popup fixed; designer and ER not driven), manual pending.** The owner ticks the boxes below; the `Result` column stays blank until then.

## Automated (done)

- `tests/Squill.Presentation.Tests/Completion/CompletionFlowTests.cs`, SQLite sample schema, no container: a real `ConnectionSession`, a real `QueryTabViewModel` with a `StubSqlEditor`, the provider the tab registers on the editor.
  - `select * from customers c join |`: the first row is `orders o` (`Detail` `ON c.id = o.customer_id`, `FilterText` `orders`); replacing the typed range with its `InsertText` gives a statement that `RunCommand` runs and that returns the 3 seeded rows.
  - Keyword case `Lower` (settings store) gives `left join` and no `LEFT JOIN`.
  - A SELECT list offers functions.
  - `create table flow_new ...` run from the tab: the new table appears in the next popup (session schema refresh).
- `tests/Shared/JoinCompletionScenario.cs` (P14-17): the same path against SQLite, PostgreSQL 17, MySQL 8.4 and SQL Server 2022, every join kind, self join, two keys, composite key, quoted names, conditions after `on `. It found and fixed a bug (unique-name table in a non-default schema was inserted unqualified).
- Avalonia headless tests of `AvaloniaSqlEditor` (existing) and the provider unit tests in `tests/Squill.Sql.Tests/Completion/` cover field mapping and ranking.

Not covered by the automated flow: the 100,000-line script sliced to the current statement (the slicing lives in `src/Squill.App.Shared/LoomSqlProviders.cs`, linked into the heads and not reachable from the test projects); everything that is the editor widget itself (below).

## Not driven

No agent session opened the WinUI or Avalonia window, typed into the editor or looked at the popup: the popup, its keyboard handling (Enter, Tab, Escape, Ctrl+Z), the documentation panel, the theme and the snippet tab stops live in LoomEditor's widget and were never driven by hand in phase 14. Everything in the checklist below is open.

## How to prepare

- SQLite: open a new SQLite profile on a copy of a file created with `tests/Shared/SampleSchema/sqlite.sql`, then seed: `insert into customers (name, email) values ('a','a@x'),('b','b@x'),('c','c@x'); insert into orders (customer_id, total) select id, 5 from customers`.
- One server engine: `rtk pwsh eng/run-docker-tests.ps1 -Engine postgres -Keep` (or `mysql`, `sqlserver`), create a profile with the printed values; tables are `sample.`-prefixed on PostgreSQL and SQL Server. At the end press Enter in that terminal, `podman ps -a` must show nothing started by you, `podman machine stop`.
- Start the WinUI head with `SQUILL_LANGUAGE=en` (then once with `fr`), then the Avalonia head for a smoke pass.

## Checklist

Columns: WinUI / Avalonia = tick when it behaves as described in that head. Result: `ok`, `fixed in <commit>`, `bug -> <card>`, `not driven (why)`. Differences between the two popups go under Notes.

| Step | WinUI | Avalonia | Result | Note |
|---|---|---|---|---|
| Type `select * from customers c join ` (the space last): the popup opens by itself, the first row is `orders o`, its detail reads `ON c.id = o.customer_id` | [ ] | [ ] | agent: ok (WinUI P20-12, dark fr, `commandes c join `: opens by itself, first row `clients cl`) | |
| Enter on that row inserts table, alias and condition at once, the caret is after the condition | [ ] | [ ] | agent: ok (WinUI P20-12: `clients cl ON c.client_id = cl.id`, caret after it) | |
| One Ctrl+Z reverts the whole insertion | [ ] | [ ] | agent: ok (WinUI P20-12: back to `... join `) | |
| The space typed to open the popup does not accept an item (P14-10 risk): type `join` then space with the list open on a keyword | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| `left join `, `right join `, `full join ` give the same condition text (SQLite: `right` / `full` only from 3.39; MySQL has no `full join` in the list) | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| `cross join ` and `natural join ` give no `ON` row | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| A table with two keys to the same table (`create table trip (id integer primary key, a integer references customers(id), b integer references customers(id))`, then `from trip t join `) gives two `customers` rows with different conditions | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Self join: a table whose key references itself gives two rows, each with an alias | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| A table without any key (`no_pk`) gets its plain row only | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Typing `ord` after `join ` filters the join rows by table name (`orders o` stays) | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Caret before an existing `ON` (`select * from customers c join | on c.id = o.customer_id`): no condition is inserted | [ ] | [ ] | | |
| Typing `on ` by hand after `join orders o` proposes the same conditions | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Server engine: a table in a non-default schema (`sample.orders`) is inserted qualified | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| A column name shared by two tables (`id` in `customers c join orders o`) is inserted qualified (`c.id`) | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Documentation panel on a table, a column, a join row and a function (`count`) shows text | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Keyword case: setting Upper, Lower, Preserve (change it in the settings, wait about five seconds, open a new popup) | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| `sel` then Tab (or Enter) expands the snippet, Tab moves between the stops, Escape leaves the snippet; `ins`, `upd`, `del`, `cte`, `crt` expand | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| No snippet is offered after `select 1 from ` | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| Light and dark theme: popup and documentation panel readable | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| A connection with about 5,000 tables (`ErStressScenario` can create one): the popup after `join ` opens without a visible stall | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |
| `SQUILL_LANGUAGE=fr`: the popup works the same (SQL keywords and details are not translated, by design) | [ ] | [ ] | agent: not driven (WinUI P20-12, time) | |

## Notes

- P19-15 (agent): Avalonia published AOT head, dark, French, SQLite. Typing `select * from cl` opens the popup by itself with `clients` (selected) and `v_ca_par_client`, a `St` kind tag, and a documentation panel (`table`, `clients - table, 5 columns`). Seen: the popup is about 520 px tall with the documentation text at the bottom and empty space between the two rows and the text, unlike a compact list. The `join` / `ON`, Enter, Tab, Ctrl+Z, snippet and keyword-case rows were not driven (time): all other Avalonia rows are `agent: not driven`. WinUI popup comparison not possible (P19-13 did not drive it); P19-13 saw a single-entry popup stay open after `Ctrl+Enter`, not seen on Avalonia (popup closed by Escape only here).
- P20-12 (agent): WinUI published AOT head, dark, French, SQLite `-Scenario Full`. `select * from cl` opens the popup by itself (`clients`, `v_ca_par_client`, kind tag `St`, selected row highlighted); no documentation panel and no footer hint line appeared on WinUI (Avalonia showed a panel in P19-15; the spec M04 text mentions a footer with keyboard chips): not investigated further. A join popup is wider than the room to the right of the caret and is cut by the window edge (about 640 px, no right border): LoomEditor's popup placement, to report to its owner.
- Stuck popup, **reproduced yes**, 3 of 3 times: type `select * from clients` (the popup stays open on the single exact entry `clients`), `Ctrl+Enter`: the query runs (12 rows) and the popup stays over the editor until `Escape`. Cause: the run keeps the editor focus and LoomEditor's suggest widget is only hidden by its own keys or a focus change. Fix in the head glue: `QueryTabView` calls `LoomSqlEditor.HideSuggest()` (command `hideSuggestWidget`) when `IsRunning` turns true, so every way to run (keys, buttons, palette) closes it; re-driven after republishing: the popup is gone after `Ctrl+Enter`. No automated test: the popup lives in the WinUI control, which the head test project cannot instantiate headless. Not driven: `Tab`, `Escape`, snippets, keyword case, light theme, English, the other rows.
