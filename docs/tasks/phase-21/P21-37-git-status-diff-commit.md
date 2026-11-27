# P21-37 - Platform and Presentation: small, optional Git support for script folders

Status: todo
Depends on: P21-34

## Goal
When a script root is inside a Git working tree, show each file's status, a diff against `HEAD`, and commit selected files with a message. Nothing more (no branches, no push, no merge): keep Git small and optional.

Exists today: `LineDiff` (P18-07) for side-by-side text; the Files panel (P21-34); no Git code.

## Design decisions (final)
- Use the **`git` executable** found on `PATH` (no libgit2: a native library and a binding would add AOT and packaging risk). Not found -> the Git features are hidden, no error.
- `IGitClient` (Platform) with `StatusAsync(root)` (`git status --porcelain=v2 -z`), `ShowHeadAsync(path)` (`git show HEAD:<path>`), `CommitAsync(root, paths, message)` (`git commit -m <message> -- <paths>` after `git add -- <paths>`); `ProcessStartInfo.ArgumentList`, no shell, 30 s timeout, stderr surfaced. The user's Git config (identity, signing, hooks) applies unchanged.
- Presentation: status letters on Files panel rows (`M`, `A`, `?`, `D`), `Compare with HEAD` opens a diff tab using `LineDiff`, `Commit...` view-model (file check list, message, `Commit`); refresh on save and on panel focus.

## Out of scope
- Push, pull, branches, stash, blame. Heads (P21-38).

## Tasks
- [ ] Parser tests over captured porcelain output; integration tests over a temp repository created with the real `git` (skipped when `git` is missing).

## Acceptance criteria
- Recipe 2 common acceptance.

## Risks
- A commit hook that waits for input: stdin closed, timeout, the message shows.

## Open points (defaults)
- Owner: is push needed (default no).

## Suggested commit message
Show Git status, diff against HEAD and commit for script folders
