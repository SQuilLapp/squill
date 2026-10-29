using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;

try
{
    var profile = new ConnectionProfile(Guid.NewGuid(), "probe", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);
    await using var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);

    await (await connection.ExecuteAsync("CREATE TABLE t (id INTEGER, name TEXT)", [], CancellationToken.None)).DisposeAsync();
    await (await connection.ExecuteAsync("INSERT INTO t VALUES (1, 'a'), (2, 'b')", [], CancellationToken.None)).DisposeAsync();

    await using var reader = await connection.ExecuteAsync("SELECT id, name FROM t", [], CancellationToken.None);
    var rows = await reader.ReadAllAsync(100, CancellationToken.None);
    Console.WriteLine($"OK {rows.Count}");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return 1;
}
