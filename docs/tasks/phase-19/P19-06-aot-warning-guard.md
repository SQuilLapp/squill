# P19-06 - Guard the AOT publish against new trim and AOT warnings

Status: done
Depends on: none

## Goal
Every head card repeats "zero IL2xxx/IL3xxx from SQuilL code, only the known third-party SqlClient / ConfigurationManager ones" (`docs/poc/aot-drivers.md`: assembly-level IL2104 for `Microsoft.Data.SqlClient`, `Microsoft.Data.SqlClient.Internal.Logging`, `System.Configuration.ConfigurationManager`, and IL3053 for `Microsoft.Data.SqlClient`). A person reads the publish log by eye each time; a new warning hidden among the known ones would pass. `Directory.Build.props` pins warnings as errors and forbids `NoWarn`, so the third-party warnings cannot be suppressed and must be recognised instead.

## Design decisions (final)
- One script `eng/check-aot-warnings.ps1` (PowerShell 7, like the other `eng/` scripts): `-Head WinUI|Avalonia|All` (default All) publishes `src/Squill.App.<Head>` with `-c Release -r win-x64` into `artifacts/aot-check/<head>` (removed in a `finally`), captures the full output, and extracts every line matching `warning IL\d{4}` into a normalized key `<code> <assembly or file>` (take the exact line format from a real publish log first; drop the path prefix and the line/column so the key is stable).
- `-LogFile <path>` skips the publish and parses an existing log (this is what the self-test and a CI step on a saved log use).
- Allowed set: `eng/aot-warnings.baseline.txt`, one key per line, `#` comments. It starts from what a real publish of each head prints today, reviewed by reading it: the file must contain only third-party assembly keys (the four above, or fewer or more if the real log differs; write the real ones and say so in the Done notes). A key whose file path is under `src/` or `tools/` is never allowed: the script fails on it even if someone adds it to the baseline.
- Exit codes: 0 only warnings that are in the baseline; 1 a warning outside the baseline (printed, with the first full log line of each); 2 the publish failed. A baseline key that no longer appears is printed as `stale baseline entry` and does not fail (a package update may fix it: then delete the line).
- Not wired into `ci.yml` (an AOT publish takes minutes and needs the WinUI workload); `docs/testing.md` gets a short "AOT warning check" section with the command. Do not add `NoWarn`, a trimmer root, or change any package.
- Optional, time-boxed (30 minutes): check whether a newer patched `Microsoft.Data.SqlClient` than 7.1.1 exists that is annotated for trimming; if one does, note the version in the Done notes and **do not** upgrade (a driver change is its own card); if not, say so.

## Out of scope
- Fixing the third-party warnings, upgrading drivers, CI wiring.

## Tasks
- [x] Script and baseline from a real publish of both heads (run in the background; each takes minutes; close every process after).
- [x] Self-test: two small fixture logs under `eng/testdata/` (`aot-clean.log` with only baseline warnings, `aot-new-warning.log` with an extra `IL2026` from a `src/` path); `pwsh eng/check-aot-warnings.ps1 -LogFile eng/testdata/aot-clean.log` exits 0 and the other exits 1. Put the two commands in `docs/testing.md`.
- [x] Run `-Head All` once for real: exit 0.

## Acceptance criteria
- The script exits 0 on the real current publish of both heads and 1 on the fixture with a new warning; `artifacts/aot-check` is removed afterwards; nothing else in the repository changed except `docs/testing.md` and the fixtures.
- Everything started is closed (`dotnet build-server shutdown`, no leftover exe).

## Risks
- The publish log format of ILC warnings differs between MSBuild and ILC lines; match on `warning IL` anywhere in the line and test with the real log, not an imagined one.
- A baseline that grows is a smell: the script prints the baseline size.

## Suggested commit message
Add a script that fails an AOT publish on a warning outside the known third-party ones

## Done notes
- Real publish log format: `<nuget path>\<assembly>.dll : warning IL2104: Assembly '...' produced trim warnings.` (MSBuild line, trailing `[project.csproj]`). A real publish of each head prints exactly four warnings, the same for both: IL2104 `Microsoft.Data.SqlClient.dll`, IL2104 `Microsoft.Data.SqlClient.Internal.Logging.dll`, IL2104 `System.Configuration.ConfigurationManager.dll`, IL3053 `Microsoft.Data.SqlClient.dll`. The baseline holds these four.
- Warnings only print when ILC really runs; a publish into a new `-o` folder with an up-to-date `obj/` printed none (the first `-Head All` run found 0 and reported four stale entries). The script therefore publishes with `-t:Rebuild;Publish`.
- `-Head All` run for real: exit 0, 4 warnings, all in the baseline; `artifacts/aot-check` removed.
- Newer SqlClient: NuGet lists 7.1.2 (and 8.0.0-preview1) after 7.1.1; whether either is trim-annotated was not checked. No upgrade made.
