using Snowflake.Data.Client;

// Compile/publish-only probe: no Snowflake account available, the connection is never opened against a server.
using var c = new SnowflakeDbConnection("account=none;user=none;password=none;host=127.0.0.1;port=1;scheme=http");
try { c.Open(); } catch (Exception e) { Console.WriteLine(e.GetType().FullName + ": " + e.Message); }
Console.WriteLine("done");
