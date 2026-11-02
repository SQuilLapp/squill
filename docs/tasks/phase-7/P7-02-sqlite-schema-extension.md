# P7-02 - Data.Sqlite: read auto-increment and foreign-key actions

Status: done
Depends on: P7-01, P1-07

## Goal
`SqliteSchemaReader` fills `ColumnDefinition.IsAutoIncrement` and `ForeignKeyInfo.OnDelete/OnUpdate`.

## Design decisions (final)
- A column is auto-increment when it is the **only** primary-key column of its table and its declared type, trimmed and case-insensitive, is exactly `INTEGER` (that is a rowid alias; `AUTOINCREMENT` keyword or not makes no difference to the model).
- Foreign-key actions come from the `on_update` and `on_delete` columns of `PRAGMA foreign_key_list`, parsed with `ReferentialActions.Parse`; unknown or missing values become `NoAction`.
- `PrimaryKeyName` stays `null`.

## Out of scope
- Other engines (P7-03 to P7-05).

## Tasks
- [x] Extend the reader.
- [x] Extend the existing `:memory:` tests: `INTEGER PRIMARY KEY` is auto-increment, `INT PRIMARY KEY` and a composite key are not, `ON DELETE CASCADE ON UPDATE SET NULL` and the default are read back.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Read auto-increment and foreign-key actions from SQLite
