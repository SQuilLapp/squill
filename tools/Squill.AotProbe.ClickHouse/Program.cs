using ClickHouse.Client.ADO;

// args[0] = connection string. Reads columns once each, prints type names.
await using var c = new ClickHouseConnection(args[0]);
await c.OpenAsync();
Console.WriteLine("server " + c.ServerVersion);
foreach (var sql in new[] { "SELECT number, toString(number), toDecimal64(number, 3), now64(3), toNullable(toUInt8(1)) FROM system.numbers LIMIT 3", "SELECT name, engine FROM system.tables WHERE database = 'system' LIMIT 3" })
{
    Console.WriteLine("> " + sql);
    await using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    await using var r = await cmd.ExecuteReaderAsync();
    for (var i = 0; i < r.FieldCount; i++) Console.WriteLine($"  col {r.GetName(i)} {r.GetDataTypeName(i)} {r.GetFieldType(i)}");
    while (await r.ReadAsync())
    {
        var o = new object[r.FieldCount]; r.GetValues(o);
        Console.WriteLine("  " + string.Join(" | ", o));
    }
}
Console.WriteLine("OK");
