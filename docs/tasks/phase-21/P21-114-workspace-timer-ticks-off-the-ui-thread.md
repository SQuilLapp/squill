# P21-114 - Workspace timer ticks updated controls from a pool thread in the Avalonia tests

Status: done
Depends on: P21-113

## Goal
Every full run of `Squill.App.Avalonia.Tests` failed one UI test with `The calling thread cannot access this object because a different thread owns it` after the project passed its 30 s mark.

## Root cause
`WorkspaceViewModel` runs a 30 s timer for its whole life and posts `RefreshElapsed` through the `IUiDispatcher`. The tests used `ImmediateDispatcher` (runs inline on the calling thread) and never dispose the workspaces they build, so the tick of a workspace left over from an earlier test ran `RefreshElapsed` on a thread-pool thread, reached `SchemaCompareViewModel.RefreshComparedText` -> `StatusBar.OnTabChanged` -> `StatusBar.RefreshEnvironment` and set `IsVisible` on a control owned by the UI thread (stack captured with `-reporter verbose`: `ImmediateDispatcher.Post` <- `WorkspaceViewModel` ctor lambda <- `TimerQueueTimer.Fire`). The exception is unhandled on the pool thread, so xunit attributed it to whichever test was running. The shipped app is not affected: `AvaloniaDispatcher` and `WinUiDispatcher` marshal to the UI thread.

## Fix
The Avalonia tests use `UiThreadDispatcher` (in `TestPlatform.cs`): inline on the UI thread, posted to the dispatcher captured at construction from any other thread, like the real dispatcher. It replaces every `ImmediateDispatcher` of the project.
