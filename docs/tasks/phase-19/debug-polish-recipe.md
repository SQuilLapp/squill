# Phase 19 recipe - debug and polish

Shared decisions for every P19 card. A card says "recipe N" for a section here. No new feature is built in this phase: every card fixes something the repository itself records as broken, unverified or known-limited, or prepares and runs verification. Audit of the repository at `ebd9820` (end of phase 18).

## 1. Where the work comes from (final)

| Source | What it says | Cards |
|---|---|---|
| `docs/verification/new-screens.md` (P18-31) | M01, M09, M10, M11, M12: no capture compared in either head or theme; the WinUI capture script copied the desktop instead of the window | P19-07 ... P19-11 |
| `docs/verification/{transactions,plan,completion,grid-filter-palette}.md`, `docs/design/visual-spec.md` section 12 | phase 13-17 screens: Avalonia never looked at, dark theme and English never read, `Failed` variant, dialogs, popup states not driven | P19-12 ... P19-15 |
| `docs/design/visual-spec.md` section 12 (M07, M08, shared styles) | designer row tags / draft state / issue badges, ER export item and hops tooltip, popup states "not driven" | P19-13, P19-15 |
| `shortcut:` comments in `src/` | `PostgresPrivilegeScript` leaves reserved words unquoted (a role named `user` breaks `GRANT`); the same gap is in `IdentifierQuoting` used by completion; the sessions and privileges lists silently stop at 400 rows | P19-01, P19-04, P19-05 |
| Card done notes P18-34, P18-31 | events raised off the UI thread (cause: Presentation raises on the caller's thread); `LargeScriptAnswersQuickly` fails under load | P19-02, P19-03 |
| Every AOT card (`docs/poc/aot-drivers.md`) | the third-party `Microsoft.Data.SqlClient` / `System.Configuration.ConfigurationManager` raise IL2104 / IL3053 on every publish; nothing stops a new warning from hiding among them | P19-06 |

Not planned (a feature, not a defect; listed in ADR 0015 and `visual-spec.md` section 10 "Not built"): projects, DuckDB, data compare, value mapping, privileges beyond PostgreSQL, the query parameters type dropdown, Oracle, the ideas in `docs/ideas.md`. Docker-gated engine scenarios already ran for PostgreSQL, MySQL and SQL Server in P18-31 (SSH in P16-04); P19-10 and P19-11 re-run only what they need.

## 2. Rules for fix cards

- Write the failing test first, see it fail, then fix, as in P18-34. A card whose bug cannot be reproduced stops with a report (Done notes) instead of changing code.
- No `NoWarn`, no `#pragma warning disable`, no `.editorconfig` exemption; CA and CS1591 rules apply as in every phase.

## 3. Rules for agent verification cards (P19-09 ... P19-15)

- Procedure of `visual-spec.md` section 11 with `tools/visual/` (P19-07 makes the capture reliable, P19-08 creates the data). `SQUILL_NO_UPDATE_CHECK=1`; theme through `<dir>/settings.json`; language through `SQUILL_LANGUAGE`. Capture at 1440 x 900.
- Always **read the PNG** (Read tool) and the composite of `Compare-Mockup.ps1`; never claim a match from a script exit code.
- For each checklist step the agent drove, write in the `Result` column `agent: ok`, `agent: bug -> <card>` or `agent: not driven (<why>)`. **Never tick a WinUI / Avalonia box**: only the owner does. Never edit a step's text.
- A difference that one small edit fixes (a token, spacing, a wording, a missing catalog key) is fixed in the card, with a test where a pattern exists. A bigger one becomes a proposed card `P19-17` onwards: the card's report names it, and the commit adds it to `docs/tasks/README.md` (index row, status `todo`) and creates the card file.
- Captures are not committed (size). The checklist Note says, per screen, which head, theme, language and mockup were compared and what differed, in text.
- Update only the `visual-spec.md` section 12 cells for what was really compared, with the card id.
- Everything started is closed: the app, containers (`podman ps -a` empty), `dotnet build-server shutdown`.
- If the window cannot be driven in a session (focus, native dialog, capture black), say so in the Note and leave the steps `agent: not driven`; that is an acceptable result, a guess is not.

## 4. Common acceptance

- Code changed: `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings (head cards also `Squill.Windows.slnx`), `rtk bypass dotnet test --solution Squill.slnx -c Release` green (the two known timing tests may be re-run alone once, see P19-03), XML doc check by grep (README conventions), catalog parity and string scanner green. Head code changed: the AOT publish of that head, zero IL2xxx/IL3xxx from SQuilL code (the known third-party ones are listed by P19-06), the app starts.
- Docs-only cards: no build needed; links must resolve.
- Close everything started.
