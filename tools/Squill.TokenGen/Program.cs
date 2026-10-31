using Squill.DesignSystem;

// usage: TokenGen <squill.tokens.json> <output folder>
if (args.Length != 2) { await Console.Error.WriteLineAsync("usage: TokenGen <tokens.json> <outDir>"); return 1; }
var tokens = XamlGenerator.Load(await File.ReadAllTextAsync(args[0]));
Directory.CreateDirectory(args[1]);
await File.WriteAllTextAsync(Path.Combine(args[1], "SquillTokens.xaml"), XamlGenerator.WinUi(tokens));
await File.WriteAllTextAsync(Path.Combine(args[1], "SquillTokens.axaml"), XamlGenerator.Avalonia(tokens));
return 0;
