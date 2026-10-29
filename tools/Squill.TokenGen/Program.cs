using Squill.DesignSystem;

// usage: TokenGen <squill.tokens.json> <output folder>
if (args.Length != 2) { Console.Error.WriteLine("usage: TokenGen <tokens.json> <outDir>"); return 1; }
var tokens = XamlGenerator.Load(File.ReadAllText(args[0]));
Directory.CreateDirectory(args[1]);
File.WriteAllText(Path.Combine(args[1], "SquillTokens.xaml"), XamlGenerator.WinUi(tokens));
File.WriteAllText(Path.Combine(args[1], "SquillTokens.axaml"), XamlGenerator.Avalonia(tokens));
return 0;
