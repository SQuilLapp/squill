using Octonica.ClickHouseClient;

await using var c = new ClickHouseConnection(args[0]);
await c.OpenAsync();
Console.WriteLine("server " + c.ServerVersion);
await using var cmd = c.CreateCommand("SELECT number, toString(number), toDecimal64(number, 3) FROM system.numbers LIMIT 3");
await using var r = await cmd.ExecuteReaderAsync();
for (var i = 0; i < r.FieldCount; i++) Console.WriteLine($"  col {r.GetName(i)} {r.GetDataTypeName(i)} {r.GetFieldType(i)}");
while (await r.ReadAsync())
{
    var o = new object[r.FieldCount]; r.GetValues(o);
    Console.WriteLine("  " + string.Join(" | ", o));
}
Console.WriteLine("OK");
