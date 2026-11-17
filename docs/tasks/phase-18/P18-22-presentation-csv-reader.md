# P18-22 - Presentation: CSV reader and preview

Status: todo
Depends on: none

## Goal
Read a CSV file the way step 1 and the preview of M10 need: encoding, delimiter, quotes, header, a sample of rows and the total row count, streaming for large files. Recipe 1.

## Design decisions (final)
- `src/Squill.Presentation/Import/` (next to the export code of P4-11; read its file API first and follow its abstractions for file access): `CsvOptions(Encoding Encoding, char Delimiter, char Quote, bool HasHeader)`, `CsvSniffer.Detect(ReadOnlySpan<byte> head)` returns suggested options: BOM then UTF-8 validity then Windows-1252 fallback (register no extra encoding provider: use UTF-8, UTF-16 and Latin-1 only, and say `Windows-1252` is read as Latin-1 in the XML doc with a `shortcut:` comment), delimiter among `, ; \t |` by consistent field count over the first 50 lines, quote `"`.
- `CsvReader` (internal-style, public type with XML doc): forward-only `IAsyncEnumerable<string?[]> ReadRowsAsync(Stream, CsvOptions, CT)`; RFC 4180 quotes (doubled quote escape, newlines inside quotes), `\r\n` and `\n`, an empty field is the empty string; the mapping layer decides what it means (P18-23). A row longer or shorter than the header is yielded as is with `FieldCountMismatch` reported through a `CsvRow(string[] Fields, long LineNumber, bool IsRagged)` record. Streams files over 1 GB without loading them.
- `CsvPreview.ReadAsync(path, options, sampleRows = 1000, CT)` returns `CsvPreview(IReadOnlyList<string> ColumnNames /* header or col1..colN */, IReadOnlyList<string?[]> Sample, long? TotalRows /* counted in the background, null until known */, long FileBytes)`; the total is counted by a second streaming pass exposed as `CountRowsAsync` so the UI shows `412 380 lignes` when it lands.
- No `Encoding.GetEncoding(int)` code pages (no provider under AOT).

## Out of scope
- JSON / Parquet / Excel, type inference beyond what P18-23 does, UI.

## Tasks
- [ ] Sniffer, reader, preview.
- [ ] Tests in `tests/Squill.Presentation.Tests/Import/`: BOM and non-BOM UTF-8, Latin-1 accents, delimiter detection for each candidate, quoted delimiter and newline inside quotes, doubled quotes, ragged rows flagged, header off gives `col1..`, empty file, 200 MB generated stream reads in bounded memory (assert peak via row-by-row enumeration, not by measuring GC), cancellation.

## Acceptance criteria
- Common acceptance (recipe 7), no head touched.

## Suggested commit message
Add the CSV reader and preview for import
