# P3-05 - Sql: tokenizer benchmark and parser ADR

Status: todo
Depends on: P3-04

## Goal
Settle risk R5 with numbers and write ADR 0003.

## Design decisions (final)
- `tools/Squill.Bench` (Exe, `BenchmarkDotNet` 0.15.8, `<IsAotCompatible>false</IsAotCompatible>` in its csproj with a comment, not published). Benchmarks: tokenize a generated 100,000-line PostgreSQL script (mix of selects with joins, inserts, comments, strings) and a 200-line script.
- `docs/adr/0003-sql-parser.md` (format of ADR 0001): decision **hand-written, error-tolerant recursive-descent/Pratt parser over per-dialect tokenizers; no ANTLR**, reasons: error recovery while typing, latency, grammar size, AOT footprint; consequences: dialect coverage grows by tests; reopening condition: the benchmark misses the budget below.
- Budget (recorded in the ADR with the measured values): tokenizing 200 lines < 1 ms; 100,000 lines < 250 ms on the dev machine. If missed, STOP and report instead of relaxing the budget.

## Out of scope
- Parser implementation (P3-06+).
- ANTLR prototype.

## Tasks
- [ ] Create the benchmark project and add it to `Squill.slnx` under `/tools/`.
- [ ] Run `rtk dotnet run -c Release --project tools/Squill.Bench -- --job short --filter *Tokenize*` and copy the results table into the ADR.
- [ ] Write the ADR.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `docs/adr/0003-sql-parser.md` exists and contains the measured numbers.

## Suggested commit message
Add tokenizer benchmark and ADR 0003 (hand-written parser)
