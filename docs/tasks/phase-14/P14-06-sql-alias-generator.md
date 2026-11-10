# P14-06 - Sql: alias generation

Status: todo
Depends on: none

## Goal
A pure generator proposes a short alias for a table that does not collide with anything visible or reserved, and decides whether an alias is needed at all.

## Design decisions (final)
- `internal static class AliasGenerator` in `src/Squill.Sql/Completion/` with `string Generate(string tableName, IReadOnlyCollection<string> taken, DatabaseDialect dialect)` and `bool IsAliasNeeded(IReadOnlyList<VisibleSource> leftSources, string? newSchema, string newTable, bool selfReference)` (policy of the recipe section 2.6: true when any left source has an alias, when `newTable` (and `newSchema` when both sides have one) is already a left source, or `selfReference`).
- Generation: split the name into words at `_`, `-`, spaces and lower-to-upper camel boundaries (`OrderItems` -> `Order`, `Items`); alias = first letter of each word, lower case (`order_items` -> `oi`, `customers` -> `c`, `HTTPLog` -> `h` then `hl`: acronyms count as one word); a name starting with a digit or symbol uses `t`. On collision (case-insensitive with `taken`, which the caller fills with existing aliases **and** existing source names, or with a reserved word of the dialect) try, in order: the first two letters of the first word plus the other initials, the first three letters, then the initials followed by `2`, `3`, ... until free. Reserved words: use the keyword set of the dialect that the tokenizer already uses (grep `Keywords.Words` in `src/Squill.Sql/Lexing`); do not copy a list. Examples that must hold: `orders` -> `o`, but `o` taken -> `or` is reserved -> `ord`; `order_items` -> `oi`; `users` with `u` taken -> `us`.
- The result is passed through `IdentifierQuoting.Quote` by the caller, not here (it is always a plain lower-case name).

## Out of scope
- Renaming existing aliases, a user-configurable alias style, using the alias elsewhere in the script.

## Tasks
- [ ] Tests first (`tests/Squill.Sql.Tests/Completion/AliasGeneratorTests.cs`): the examples above, camelCase, acronym, digits/leading symbol, collision chain to `2`, reserved words per dialect (`or`, `on`, `as`, `by`, `is`, `in` never produced; one two-letter initials result that is reserved in one dialect's set and free in another, picked by reading the sets, asserted for both dialects), case-insensitive `taken`; `IsAliasNeeded` for each of the three reasons and for the all-false case (`customers c` visible and `orders` new -> true because of the alias; `customers` visible without alias and `orders` new -> false; `orders` visible and `orders` new -> true; schema compared only when both given).
- [ ] Implement, XML docs.

## Acceptance criteria
- Recipe section 4.

## Suggested commit message
Generate table aliases for joins
