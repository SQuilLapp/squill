# P12-10 - Presentation: release notes model (markdown subset)

Status: todo
Depends on: P12-04

## Goal
Release notes arrive as markdown (from `CHANGELOG.md` through Velopack). Both heads must show them without a markdown or HTML control: Presentation parses the subset defined in P12-04 into a flat list of blocks that each head renders with plain text elements.

## Design decisions (final)
- Namespace `Squill.Presentation.Updates`.
- `ReleaseNotes.Parse(string? markdown)` (public static) returns `IReadOnlyList<NotesBlock>`; `NotesBlock` is a record hierarchy: `NotesHeading(string Text)`, `NotesParagraph(IReadOnlyList<NotesRun> Runs)`, `NotesBullet(IReadOnlyList<NotesRun> Runs)`; `NotesRun(string Text, NotesRunStyle Style, string? Url)` with `enum NotesRunStyle { Plain, Bold, Code, Link }`.
- Grammar (anything else is kept as plain text, never dropped, never throws): lines starting with `#`..`###` (any count of `#`) are headings; lines starting with `- ` or `* ` are bullets (a continuation line indented by two spaces joins the previous bullet with a space); blank line ends a paragraph; consecutive text lines form one paragraph joined by a space; inline `**bold**`, `` `code` `` and `[text](url)`. A link is a `Link` run only when `url` is an absolute `https://` or `http://` URI (`Uri.TryCreate`); any other scheme (`javascript:`, `file:`, `ms-settings:`, relative) becomes a `Plain` run showing `text (url)`. Unclosed markers are plain text. `\r\n` and `\n` both accepted. Input longer than 64 KiB is cut at the last full line before the limit.
- `NotesBlock` text is never translated; headings such as `Added` are shown as written (the changelog is English by repository rule; fr/en UI around it is translated elsewhere).
- Heads render links by opening the URL with the OS shell through the existing `IDialogService`/launcher abstraction if one exists; this card does not add one (P12-13/14/15 decide); the parser only guarantees the URL is http(s).

## Out of scope
- Rendering, the update view-model, HTML notes.

## Tasks
- [ ] `ReleaseNotes`, `NotesBlock` records, `NotesRun`, `NotesRunStyle`, XML docs.
- [ ] Tests in `tests/Squill.Presentation.Tests/Updates/`: the seeded `0.1.0` body of `CHANGELOG.md` (copied as a string constant into the test, not read from disk) parses into the expected blocks; each inline style; nested markers (`**a `b`**` stays one bold run containing the backticks as text); unclosed markers; link schemes table (https ok, `javascript:alert(1)`, `file:///c:/x`, relative, `ms-settings:` -> plain); CRLF; empty/null -> empty list; 100 KiB input truncated; a `|` table row and `<b>` stay plain text (no HTML interpretation).

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.
- Property-style test: parsing random printable ASCII strings (seeded, 2,000 cases) never throws and never produces a `Link` run with a non-http(s) URL.

## Suggested commit message
Add a markdown-subset release notes parser to Presentation
