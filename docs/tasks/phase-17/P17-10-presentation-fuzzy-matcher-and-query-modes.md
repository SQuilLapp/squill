# P17-10 - Presentation: fuzzy matcher and palette query modes

Status: todo
Depends on: none

## Goal
The two pure pieces under the palette search: a scored subsequence matcher that reports the matched characters, and the parser that turns the typed text into a mode and a search term. Recipe decisions 11 and 12.

## Design decisions (final)
- New folder `src/Squill.Presentation/Palette/`. `FuzzyMatcher.Match(string candidate, string pattern)` returns `FuzzyMatch?` (`sealed record FuzzyMatch(int Score, IReadOnlyList<(int Start, int Length)> Ranges)`; ranges are merged runs of matched characters in candidate indexes, for the head to color). Rules: case-insensitive and accent-insensitive (compare after removing diacritics through `string.Normalize(FormD)` mapping, keeping index mapping to the original string correct: build the folded string and an index map, do not assume equal lengths); a pattern with whitespace matches each word independently in order; empty pattern returns a match with score 0 and no ranges; no match returns `null`. Scoring: exact (whole string) > prefix > match starting at a word start (after space, `_`, `.`, `-`, `/`, or a lower-to-upper camel boundary) > consecutive run > scattered subsequence; bonuses for consecutive characters and word-start hits, a small penalty per skipped character and for a long candidate; the best of the greedy alignments is enough (no need for optimal alignment; document the shortcut with a `shortcut:` comment: greedy with one retry from the best word start, upgrade if ordering feels wrong). Candidates up to 256 characters are scored; longer ones are cut to that length.
- `PaletteMode` enum: `All`, `Actions`, `Objects`, `Line`, `History`. `PaletteQuery.Parse(string text)` returns `(PaletteMode Mode, string Term, string Prefix)`: first character `>` `#` `:` `!` selects the mode (`@` is not a mode: it is plain text), the term is the rest trimmed of leading spaces, `Prefix` is the character used (empty for `All`). `PaletteQuery.WithMode(string text, PaletteMode mode)` rewrites the typed text for a mode pill click (replaces an existing prefix, keeps the term). For `Line` the term must be digits (`:12`); `PaletteQuery.TryLine(term, out int line)`.
- The matcher is `static`, allocation-light (spans, a pooled buffer or `stackalloc` for short candidates), thread safe (no shared state): P17-12 calls it from worker threads for 50,000 names.

## Out of scope
- Items, providers, view-model (P17-11 ...), highlighting (heads).

## Tasks
- [ ] `FuzzyMatcher`, `FuzzyMatch`, `PaletteMode`, `PaletteQuery`.
- [ ] Tests `tests/Squill.Presentation.Tests/Palette/FuzzyMatcherTests.cs`, `PaletteQueryTests.cs`: the M13 examples (`comm` matches `commandes`, `lignes_commande`, `v_commandes_recentes`, `Commit — valider la transaction`, `Commenter / décommenter la ligne`, `Comparer les schémas...` with the expected ranges for the first and last), ranking order (exact, prefix, word start, scattered), accents (`ecole` matches `École`, ranges point at the original string), case, words in order (`cmd tab` vs `tab cmd`), empty pattern, no match, a 10,000-character candidate does not blow up, every prefix and `WithMode` round trip, `:12` and `:abc`, `@x` is text.

## Acceptance criteria
- Common acceptance of the recipe (section 7), no head touched; matching 50,000 names of 30 characters with a 4-letter pattern stays under a generous bound in a test (not a benchmark).

## Suggested commit message
Add the palette fuzzy matcher and query modes
