# ADR 0003 - SQL parser

- Status: accepted
- Date: 2026-10-29

## Context
Risk R5: the editor needs completion, diagnostics, hover and formatting on SQL that is almost always incomplete while the user types, for four dialects. The options were a generated parser (ANTLR) or a hand-written one.

## Decision
**Hand-written, error-tolerant recursive-descent / Pratt parser over per-dialect tokenizers; no ANTLR.**

Reasons:
- Error recovery while typing: a hand-written parser can resynchronize on statement and clause keywords and always return a partial tree.
- Latency: the tokenizer alone fits the budget below with a wide margin, leaving room for parsing on every keystroke.
- Grammar size: full ANTLR grammars for four dialects are large; the editor only needs the statements and clauses the cards cover.
- AOT footprint: no runtime library, no reflection, trim-safe by construction.

## Measurements
Budget: tokenizing 200 lines < 1 ms, 100,000 lines < 250 ms.

`rtk dotnet run -c Release --project tools/Squill.Bench -- --job short --inProcess --filter *Tokenize*` (PostgreSQL tokenizer, .NET 11.0.0-rc.3, dev machine, generated script mixing joins, inserts, comments and strings):

| Method              | Mean         | Error        | StdDev       | Allocated   |
|-------------------- |-------------:|-------------:|-------------:|------------:|
| Tokenize200Lines    |     84.20 us |     30.92 us |     1.695 us |   133.13 KB |
| Tokenize100000Lines | 47,268.55 us | 24,987.10 us | 1,369.627 us | 67053.89 KB |

Both are within budget (0.084 ms vs 1 ms; 47 ms vs 250 ms). `--inProcess` is required because BenchmarkDotNet 0.15.8 does not recognize the net11.0 runtime moniker and throws in its default out-of-process toolchain. The `short` job gives wide error bars; the margin is large enough for the verdict not to depend on them.

## Consequences
- Dialect coverage grows by tests, one construct at a time.
- Parser work (P3-06 onward) must keep the tolerant-tree contract: never throw, always return a tree.
- Reopening condition: the benchmark misses the budget above, or the parser cannot reach an acceptable recovery quality on real typing sessions.
