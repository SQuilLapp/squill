using Microsoft.Data.SqlClient;

try
{
    if (args.Length != 1)
    {
        Console.WriteLine("usage: Squill.AotProbe.SqlServer <connection string>");
        return 2;
    }

    await using var connection = new SqlConnection(args[0]);
    await connection.OpenAsync();
    await using var command = new SqlCommand("select 1 as a, 'x' as b", connection);
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
