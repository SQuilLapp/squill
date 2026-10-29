---
name: next-task
description: Implement the next SQuilL task card (or the one given), verify it (build, tests, AOT), update the card and index, then make a signed commit and push.
argument-hint: "[card id, e.g. P1-03 - optional]"
---

# Implement the next task card

Requested card: `$ARGUMENTS` (empty = pick the next one automatically).

Follow these steps **in order**. Stop and report to the user as soon as a step says "stop". Everything written in the
repository (code, docs, commit message) is in **English**; the report to the user can be in the user's language.
**Every shell command goes through `rtk`** (`rtk dotnet build ...`, `rtk git status`; `rtk bypass <cmd>` only when the
condensed output is unusable, and always for `dotnet test`).

## 1. Check the starting point

1. `rtk git status --porcelain`. If anything is modified or untracked, **stop**: the previous card must be committed
   first, so that each card is one commit.
2. Never create or switch branches: work on the current branch.

## 2. Pick the card

- If `$ARGUMENTS` names a card (e.g. `P1-03`), use `docs/tasks/phase-*/<id>-*.md`.
- Otherwise open `docs/tasks/README.md` and take the **first** row, in table order, whose status is `todo` and whose
  "Depends on" cards are all `done` (a range such as "P1-01 ... P1-05" means every card in it).
- If no card qualifies, **stop** and say so. If the requested card's dependencies are not all `done`, **stop** and list them.

Tell the user which card you picked and why, in one sentence, then continue without waiting.

## 3. Implement it

1. Read `docs/tasks/README.md` (conventions), `docs/architecture.md`, the ADRs the card mentions, and the card.
   "Design decisions" are final; "Out of scope" is forbidden territory. Do not touch files the card does not need.
2. Set the card's `Status:` to `in progress` and the index row to `in progress`.
3. Write the tests first, then the production code, exactly as the card describes. Never invent an API of an external
   package or of LoomEditor/LoomTerminal: read the package or the checkout.
4. If the card is ambiguous, contradicts the code base, or cannot be done as written (including its own "STOP" rules),
   **stop** without committing: explain the problem and propose a fix to the card.

## 4. Verify

From the repository root, use `Squill.Windows.slnx` instead of `Squill.slnx` when the card says so:

```
rtk dotnet build Squill.slnx -c Release -warnaserror
rtk bypass dotnet test --solution Squill.slnx -c Release
```

plus every extra command of the card's acceptance criteria, in particular each Native AOT
`rtk dotnet publish <project> -c Release -r win-x64` (zero IL2xxx/IL3xxx warnings) and the run of the published exe
when the card asks for it. Required: **0 warnings, 0 errors, all tests green**. Fix and re-run until they pass; if you
cannot, **stop** and report the failures with their output.

Never weaken a test, never add `#pragma warning disable` or `NoWarn` to get green unless the card says so.

## 5. Update the bookkeeping

- Card: tick every finished checkbox, set `Status: done`.
- Index (`docs/tasks/README.md`): set the card's row to `done`.
- Anything the card asks to record (`docs/architecture.md`, `docs/poc/*.md`, ADRs).

## 6. Close everything you opened

Before committing (and before any "stop" above), **force-close every process you launched** for the card: published
exes and sample apps, dev servers, background `dotnet` hosts (`rtk dotnet build-server shutdown`), Docker containers
you started, preview/browser tabs, background shell tasks.

1. Stop what you started yourself first (background tasks, `preview_stop`, terminal tabs you opened).
2. Then look for leftovers started from this repository, e.g.
   `rtk bypass pwsh -NoProfile -Command "Get-Process | Where-Object { $_.Path -like '*squill*' } | Select-Object Id,ProcessName,Path"`,
   and kill them (`Stop-Process -Id <id> -Force`). Only kill processes you started or that run from this repository;
   never touch the user's own terminals or unrelated `dotnet`/shell processes.
3. Mention in the report what was closed (or "nothing left running").

## 7. Commit and push

1. `rtk git status` and `rtk git diff --stat`: only files related to this card changed (no `bin/`, `obj/`, `artifacts/`).
2. Stage the files explicitly (`rtk git add <paths>`), never `git add -A` blindly.
3. Commit **signed** (the GPG key of adrien.granger@outlook.com is already configured: do not pass `-S` options that
   change it, never `--no-gpg-sign`, never `--no-verify`). Subject = the card's "Suggested commit message" (adjusted only
   if it no longer describes the change), body of 2-5 lines saying what was added and which tests, then the attribution
   trailer required by the session. If signing fails, **stop** and report (do not commit unsigned).
4. Push right after the commit: `rtk git push` (first push of a branch: `rtk git push -u origin HEAD`). If the push is
   rejected, **stop** and report; never force-push.
5. Verify with `rtk git log -1 --format='%G? %h %s'` that the signature status is `G`.

## 8. Report

Summarize in a few lines: card done, commit hash, number of tests, anything the next card should know (deviations, pinned
versions that differed from `docs/architecture.md`, AOT verdicts), and the next card in line.
