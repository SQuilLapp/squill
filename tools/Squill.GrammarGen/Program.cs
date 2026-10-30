using Squill.Core;
using Squill.Sql.Highlighting;

// usage: GrammarGen <output folder>
if (args.Length != 1) { Console.Error.WriteLine("usage: GrammarGen <outDir>"); return 1; }
Directory.CreateDirectory(args[0]);
foreach (var (dialect, file) in new[]
{
    (DatabaseDialect.PostgreSql, "postgresql"), (DatabaseDialect.MySql, "mysql"),
    (DatabaseDialect.SqlServer, "tsql"), (DatabaseDialect.Sqlite, "sqlite"),
})
{
    File.WriteAllText(Path.Combine(args[0], $"sql-{file}.tmLanguage.json"), SqlGrammarGenerator.Generate(dialect));
}

return 0;
