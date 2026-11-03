# P10-15 - Migrate strings: history, snippets, search box and terminal tabs

Status: done
Depends on: P10-03, P10-04, P10-07, P10-08

## Goal
The history and snippets panels, their prompts and confirmations, and the terminal tab chrome are fully bilingual in both heads.

## Design decisions (final)
Follow `docs/tasks/phase-10/migration-recipe.md`. Area `history-snippets-terminal` of `inventory.md`.
- Files in scope (the inventory is authoritative): `src/Squill.Presentation/Panels/HistoryViewModel.cs` ("Clear history" confirmation), `SnippetsViewModel.cs` ("Save snippet", "Rename snippet", "Snippet name" prompt, delete confirmation), `HistoryView`, `SnippetsView`, `HistoryFormat` (Avalonia) and its WinUI counterpart (relative-time words such as "just now", "yesterday": catalog keys with `FormatPlural` for "N minutes ago"; the absolute date part uses the culture of P10-07), `TerminalTabViewModel` captions, `TerminalTabView`, `LoomTerminalHost` messages and the search box (command search placeholder only; the command palette itself is a later feature).
- Snippet names, history SQL text and shell output are user data: not translated. Built-in snippet bodies (`SqlSnippets`) are SQL; their display names, if any, are catalog keys (decide from the code and say which).
- The terminal's shell names and paths are data.

## Out of scope
- Workspace "terminal unavailable" errors (P10-16).

## Tasks
- [x] Migrate every `history-snippets-terminal` inventory line and tick it.
- [x] Tests per recipe rule 7 (relative-time and plural wording in both languages, each prompt and confirmation).
- [x] English captures: history list (today / older entries), snippets list, rename prompt, terminal tab, both heads.

## Acceptance criteria
- Common criteria of the recipe.

## Suggested commit message
Localize the history, snippets and terminal panels
