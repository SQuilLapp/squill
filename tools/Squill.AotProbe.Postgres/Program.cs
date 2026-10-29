using Npgsql;

try
{
    if (args.Length != 1)
    {
        Console.WriteLine("usage: Squill.AotProbe.Postgres <connection string>");
        return 2;
    }

    await using var dataSource = new NpgsqlSlimDataSourceBuilder(args[0]).Build();
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var command = new NpgsqlCommand("select 1 as a, 'x' as b", connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        Console.WriteLine($"{reader.GetValue(0)} {reader.GetValue(1)}");
    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return 1;
}
