#:package Microsoft.Data.Sqlite@11.0.0-rc.3.26522.148

using Microsoft.Data.Sqlite;

// Fallback of New-VisualProfile.ps1 when the sqlite3 CLI is missing: Seed.cs <database file> <seed.sql>.
using var connection = new SqliteConnection($"Data Source={args[0]}");
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = File.ReadAllText(args[1]);
command.ExecuteNonQuery();
