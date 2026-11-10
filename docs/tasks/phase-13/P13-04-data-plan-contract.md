# P13-04 - Data: plan reader contract

Status: done
Depends on: P13-01

## Goal
The provider-facing contract that returns a `QueryPlan`, and the fake used by Presentation tests.

## Design decisions (final)
- `public interface IPlanReader` in `Squill.Data`: `ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)` and `bool SupportsActualPlan { get; }`. It is a **separate interface**, not a member of `IDatabaseConnection`, so `Squill.Data.Oracle` and any other implementer stay untouched; callers do `connection as IPlanReader`.
- `public sealed class PlanUnavailableException(string reasonCode, string message) : Exception` (`ReasonCode` property: `parameters`, `permission`, `version`, `engine`); `public sealed class PlanParseException(string message, string sourceText, Exception? inner = null) : Exception` (`SourceText` kept so the UI can show it in `Texte`). Both XML documented, CA2237-safe (no serialization ctor needed on modern .NET; check the analyzers).
- The contract says: the statement passed is one statement, already vetted by `PlanEligibility`; the reader never executes it for `Estimated`; for `Actual` it executes it and `RolledBack` tells whether a rolled-back transaction wrapped it; cancellation interrupts like a normal run (`P11-10` behaviour) and leaves the connection usable (see recipe risk 1).
- `FakeDatabaseProvider` (find it with grep; Presentation tests use it) gets a fake connection that also implements `IPlanReader`, returning a configurable `QueryPlan` or throwing a configured exception, and recording the calls (sql, mode).

## Out of scope
- Any real engine (P13-05 ... P13-10).

## Tasks
- [x] Interface, two exceptions, fake, XML docs.
- [x] `tests/Squill.Data.Tests`: the fake returns the configured plan and records the mode; exceptions carry reason and source text.

## Acceptance criteria
- Common acceptance of the recipe, section 5. Oracle project builds without change.

## Suggested commit message
Add the plan reader contract to Data
