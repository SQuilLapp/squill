# P21-94 - AI assistant (3/5): Presentation (generate SQL, explain, fix an error)

Status: todo
Depends on: P21-93, P21-30

## Goal
Three actions in the query tab: **Generate SQL** from a prompt, **Explain** the selected SQL, **Fix** the statement that failed (with the engine error and its position from P21-30). The answer is shown as a proposal, never run automatically.

Exists today: query tab, script report with error positions (P21-30), diagnostics, the palette action registry.

## Design decisions (final)
- `AssistantViewModel` per query tab: prompt box, action, the preview step (P21-93) with `Send` / `Cancel`, a busy state with cancel, the reply split into text and SQL blocks; `Insert at caret`, `Replace selection`, `Copy`; for `Fix`, a diff with the original statement (`LineDiff`) and `Apply` (as one undo step).
- **Never executes** generated SQL; inserted SQL goes through the normal run path (production confirmation, transactions) when the user runs it.
- Disabled state with the reason when `AssistantSettings.Enabled` is false or no key is stored; a `Settings...` link.
- Conversation kept in memory per tab for follow-ups (last 6 exchanges), cleared on tab close; nothing written to disk except what the user inserts.
- Palette actions `AI: Generate SQL`, `AI: Explain`, `AI: Fix error`.

## Out of scope
- Heads (P21-95, P21-96), inline completion (ghost text), agents running statements.

## Tests and tasks
- [ ] Tests with a fake provider: each action's request (through the preview), cancel, errors shown, insert/replace/apply edits, never-executes guard (no call to the runner), disabled reasons.

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A reply with several SQL blocks: offer each block separately.

## Open points (defaults)
- None.

## Suggested commit message
Add the AI assistant view-model to generate, explain and fix SQL with a confirmed preview
