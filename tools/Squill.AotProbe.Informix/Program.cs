using IBM.Data.Db2;

// usage: Squill.AotProbe.Informix "<connection string>"   (DRDA listener, e.g. Server=localhost:9089;Database=probe;UID=informix;PWD=...)
try
{
    if (args.Length != 1)
    {
        Console.WriteLine("usage: Squill.AotProbe.Informix <connection string>");
        return 2;
    }

    using var connection = new DB2Connection(args[0]);
    connection.Open();
    Console.WriteLine("server " + connection.ServerVersion);
    Query(connection, "SELECT FIRST 5 tabname FROM systables");
    using (var insert = new DB2Command("INSERT INTO t(d, b) VALUES (?, ?)", connection))
    {
        insert.Parameters.Add(new DB2Parameter("d", 1.5m));
        insert.Parameters.Add(new DB2Parameter("b", new byte[] { 1, 2, 3 }));
        Console.WriteLine("inserted " + insert.ExecuteNonQuery());
    }

    Query(connection, "SELECT id, d, ts, lv, b FROM t");
    Console.WriteLine("OK");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    return 1;
}

static void Query(DB2Connection connection, string sql)
{
    Console.WriteLine("> " + sql);
    using var command = new DB2Command(sql, connection);
    using var reader = command.ExecuteReader();
    for (var i = 0; i < reader.FieldCount; i++)
        Console.WriteLine($"  col {reader.GetName(i)} {reader.GetDataTypeName(i)} {reader.GetFieldType(i)}");
    while (reader.Read())
    {
        var cells = new string[reader.FieldCount];
        for (var i = 0; i < cells.Length; i++)
            {
            if (reader.IsDBNull(i)) { cells[i] = "NULL"; continue; }
            var v = reader.GetValue(i);
            cells[i] = $"{Show(v)} ({v.GetType().Name})";
        }
        Console.WriteLine("  " + string.Join(" | ", cells));
    }
}

static string Show(object value) => value switch
{
    DateTime dt => dt.ToString("o"),
    byte[] bytes => Convert.ToHexString(bytes),
    _ => value.ToString() ?? "",
};
