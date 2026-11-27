# P21-12 - Sql: Informix dialect (tokenizer, splitter, keywords, quoting, types, functions)

Status: todo
Depends on: P21-106

## Goal
Highlighting, splitting, completion and hover for Informix instead of the generic fallback.

Exists today: per-dialect tokenizers and catalogues (see P21-05 for Oracle); since P21-106 an `InformixTokenizer` exists (`{ }` comments, `?` and `:name` parameters, ANSI keywords only): extend it instead of writing a new one. The editor still underlines `FIRST` in `select first 5 ...` and treats the statement as read-only (`EditableSourceAnalyzer`).

## Design decisions (final)
- Tokenizer: `{ ... }` comments in addition to `--` and `/* */`; `"..."` is a string unless `DELIMIDENT` is set (default: treat `"` as identifier quotes, the DRDA default; record the choice in the card); `?` parameters.
- Splitter: `CREATE PROCEDURE|FUNCTION ... END PROCEDURE|FUNCTION;` is one statement.
- Keywords and functions: `FIRST n`, `SKIP n`, `MATCHES`, `NVL`, `DECODE`, `TODAY`, `CURRENT YEAR TO SECOND`, `MDY`, `EXTEND`.
- Types: `SERIAL`, `SERIAL8`, `BIGSERIAL`, `INT8`, `DECIMAL(p,s)`, `MONEY`, `LVARCHAR`, `DATETIME <q> TO <q>`, `INTERVAL`, `BYTE`, `TEXT`, `BLOB`, `CLOB`, `BOOLEAN`.
- Quoting: lower-case folding.

## Out of scope
- DDL generator, formatter rules beyond keywords.

## Tasks
- [ ] Tokenizer, splitter, quoting, catalogue tests; grammar regenerated.
- [ ] Corpus of 15 statements accepted by the container (Docker-gated).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- `{` comments clash with nothing in other dialects, but the tokenizer base must keep them Informix-only.

## Open points (defaults)
- `DELIMIDENT` default as above.

## Suggested commit message
Add the Informix dialect to the SQL tokenizer, splitter and catalogues
