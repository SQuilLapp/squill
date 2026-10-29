# SQuilL - agent guidelines

SQL client on .NET 11 (RC3), Native AOT, WinUI 3 / Avalonia. Task cards live in `docs/tasks/` and are implemented one at a time with the `next-task` skill (`.claude/skills/next-task/`).

## Language and code

- Everything in the repository (code, comments, docs, commit messages) is in **English**.
- **C# 15** (`LangVersion` is pinned to `15` in `Directory.Build.props`). Follow .NET best practices and the conventions in `docs/tasks/README.md`.
- **XML documentation on every public and protected type and member** (CS1591 is an error). XML doc also on every member declared `public` inside an `internal` type, and a one-line `<summary>` on every `internal` type (the compiler does not enforce this; check with a grep before each commit). Write useful `<summary>` text, and `<param>`, `<returns>`, `<exception>` where they add information. Never repeat the member name.
- Warnings are errors, AOT/trim analyzers are on. Never add `NoWarn`, `#pragma warning disable` or an `.editorconfig` exemption to get green. If a card forces a violation, stop and propose a card fix.
- No reflection, JSON only through source-generated contexts, public API names must satisfy the CA rules (for example no type name ending in `Stream` unless it derives from `Stream`).

## Git

- **One branch per phase** (phase 1: `phase-1-foundations`), checked out in `D:\squill`. Never create extra branches or worktrees for cards or sessions; start sub-sessions without a worktree.
- One card = one commit, **GPG-signed** with the key of adrien.granger@outlook.com (already configured), and **every commit is pushed** (`git push origin <phase branch>`). Never force-push.
- **No `Co-Authored-By` trailer and no "Generated with" line in any commit message or PR description.**
- Never delete a branch or session without being asked. Never rewrite pushed history without being asked.

## Shell and processes

- Every shell command goes through `rtk` (`rtk bypass <cmd>` only when the filtered output is unusable, for example `rtk bypass dotnet test`).
- Verify with `rtk bypass dotnet build Squill.slnx -c Release -warnaserror` (0 warnings), `rtk bypass dotnet test`, and a Native AOT publish when the card asks for it.
- Before moving to the next card, close everything you started (`dotnet build-server shutdown`, AOT executables, containers, servers, tabs).

## Working with sessions

- Run `/next-task` in a fresh Sonnet 5.5 session and stop it when its context passes 50 %; start the next session before asking to delete the finished one.
- Creating new task cards is done by an Opus 5.5 sub-session, each card sized so Sonnet 5.5 can implement it alone.
- A card that is ambiguous or impossible as written stops with a report and a proposed card fix.
