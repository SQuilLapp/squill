# P1-05 - Data: provider contracts and registry

Status: todo
Depends on: P1-04

## Goal
Define the abstraction every database provider implements, with a fake used by later tests.

## Design decisions (final)
- Project `src/Squill.Data` (references `Squill.Core`), namespace `Squill.Data`. Test project `tests/Squill.Data.Tests` with an internal `FakeDatabaseProvider` (scripted columns and rows).
- `interface IDatabaseProvider { DatabaseDialect Dialect { get; } ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken); }`.
- `interface IDatabaseConnection : IAsyncDisposable { string ServerVersion { get; } ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken); ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken); }`. One call executes exactly one statement (script splitting is `Squill.Sql`).
- `interface IResultReader : IAsyncDisposable { IReadOnlyList<ColumnInfo> Columns { get; } long RecordsAffected { get; } ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken); }` returns `null` at the end; statements without a result set have empty `Columns`. `RecordsAffected` is -1 until known.
- `sealed record QueryParameter(string Name, object? Value)`.
- `sealed class ProviderRegistry { void Register(IDatabaseProvider p); IDatabaseProvider Get(DatabaseDialect d); }` throws `NotSupportedException` with the dialect name if missing; registering twice for one dialect throws `InvalidOperationException`.
- Extension `ResultReaderExtensions.ReadAllAsync(this IResultReader, int batchSize, CancellationToken)` returning `List<object?[]>` (test helper reused by provider tests).

## Out of scope
- Concrete providers.
- Connection pooling (providers use their driver's pooling).

## Tasks
- [ ] Create the project, the contracts, the registry and the extension.
- [ ] Add `Squill.Data` and `Squill.Data.Tests` to `Squill.slnx`.
- [ ] Tests: registry behaviours, `ReadAllAsync` with the fake.

## Acceptance criteria
- `rtk dotnet build Squill.slnx -c Release -warnaserror` exits 0 with 0 warnings.
- `rtk bypass dotnet test --solution Squill.slnx -c Release` is green (new tests included, none skipped except Docker-gated ones).

## Suggested commit message
Add Data provider contracts and registry
