# P12-03 - Platform: variable store and secret keys

Status: todo
Depends on: P12-01

## Goal
Persist variable definitions per scope; secret values go to the secret store. Specification: [variables-spec.md](variables-spec.md) section 4.

## Design decisions (final)
- `IVariableStore` (`Squill.Platform.Variables`): `ListAsync(VariableScope scope, string? ownerId, ct)` returns definitions without secret values; `ListAllAsync(ct)`; `SaveAsync(scope, ownerId, VariableDefinition definition, string? secretValue, ct)` (`secretValue` null keeps the stored one, empty deletes it, same convention as `IProfileStore.SaveAsync`); `RenameAsync(scope, ownerId, oldName, newName, ct)` (moves the secret key too, fails when the new name exists); `DeleteAsync(scope, ownerId, name, ct)`; `DeleteOwnerAsync(scope, ownerId, ct)` (used when a profile is deleted); `GetSecretAsync(scope, ownerId, name, ct)`.
- `Tab` is never persisted: `SaveAsync` with `Tab` throws `ArgumentException`.
- `JsonVariableStore` over `JsonFileStore<T>` (internal; read it first) with a source-generated JSON context (add to `StorageJsonContext` if that is the pattern); file `variables.json` through `AppPaths`.
- `SecretKeys.Variable(VariableScope scope, string? ownerId, string name)` in `Squill.Core` returns `variable:<scope lower>:<ownerId or ->:<name>`. Uniqueness per (scope, ownerId, name).
- Environment owner id is the `ConnectionEnvironment` enum name; `None` is rejected.
- No events here (Presentation raises change notifications, P12-04).

## Out of scope
- UI, resolution across scopes (P12-04).

## Tasks
- [ ] Store, key helper, JSON context, XML doc.
- [ ] Tests (`tests/Squill.Platform.Tests/Variables/`, `InMemorySecretStore`, temp directory): round trip per scope, secret string absent from the JSON file text, keep/delete secret semantics, rename moves the secret, rename onto an existing name fails, `DeleteOwnerAsync` removes definitions and secrets, `Tab` rejected, corrupt file recovered, concurrent saves.

## Acceptance criteria
- Build 0 warnings; tests green; AOT analyzers silent (source-generated JSON only).

## Suggested commit message
Persist query variables with secrets in the secret store
