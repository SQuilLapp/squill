using Squill.Core;
using Squill.Sql.Highlighting;

// usage: GrammarGen <output folder>
if (args.Length != 1) { await Console.Error.WriteLineAsync("usage: GrammarGen <outDir>"); return 1; }
Directory.CreateDirectory(args[0]);
foreach (var (dialect, file) in new[]
{
    (DatabaseDialect.PostgreSql, "postgresql"), (DatabaseDialect.MySql, "mysql"),
    (DatabaseDialect.SqlServer, "tsql"), (DatabaseDialect.Sqlite, "sqlite"),
})
{
    await File.WriteAllTextAsync(Path.Combine(args[0], $"sql-{file}.tmLanguage.json"), SqlGrammarGenerator.Generate(dialect));
}

return 0;
