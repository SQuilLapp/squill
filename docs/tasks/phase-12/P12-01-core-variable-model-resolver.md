# P12-01 - Core: variable model and resolver

Status: todo
Depends on: none

## Goal
Pure model of query variables and the function that resolves a name against the scopes, so every later card (store, editor colouring, substitution) shares one source of truth. Specification: [variables-spec.md](variables-spec.md) sections 2 to 4.

## Design decisions (final)
- Namespace `Squill.Core.Variables`, no I/O, no secrets handling beyond a flag.
- `VariableScope` enum, lowest to highest precedence: `Global`, `Environment`, `Connection`, `Tab`.
- `VariableDefinition(string Name, string? Value, bool IsSecret, bool AllowRaw, string? Description)` record; `Value` is `null` for a secret (its value lives in the secret store).
- `VariableValue(string Value, bool IsSecret, bool AllowRaw)` and `VariableLayer(VariableScope Scope, IReadOnlyDictionary<string, VariableValue> Values)`; layers are built by the caller (Presentation, P12-04) and already contain secret values.
- `VariableName` static class: `IsValid(string)` (rule of spec section 2: `[A-Za-z_][A-Za-z0-9_.-]*`, at most 64 chars) and `MaxLength`.
- `VariableResolver.Resolve(string name, IReadOnlyList<VariableLayer> layers)` returns `ResolvedVariable(string Name, VariableState State, string? Value, VariableScope? Scope, bool IsSecret, bool AllowRaw, IReadOnlyList<VariableScope> Shadowed)`; `VariableState` is `Resolved` or `Unresolved`. Highest scope wins; `Shadowed` lists the lower scopes that also define the name, highest first.
- `ResolvedVariable.ToString()` is overridden and never prints the value (tested), so a secret cannot leak into a log by accident. Display masking belongs to Presentation (P12-07).
- Names are compared ordinal (case-sensitive).

## Out of scope
- Scanning SQL text (P12-02), persistence (P12-03), UI.

## Tasks
- [ ] Types above, XML doc on every public member (never repeat the member name).
- [ ] `tests/Squill.Core.Tests/Variables/`: precedence for each pair of scopes, shadowed list, unresolved, invalid names (empty, leading digit, 65 chars, space, `$`), case sensitivity, `ToString` of a secret does not contain the value.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` 0 warnings; `rtk bypass dotnet test --solution Squill.slnx -c Release` green.

## Suggested commit message
Add the query variable model and resolver to Core
