using System.Text;
using BenchmarkDotNet.Attributes;
using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Bench;

/// <summary>Measures PostgreSQL tokenization on a small and a very large generated script.</summary>
[MemoryDiagnoser]
public class TokenizeBenchmarks
{
    private static readonly ISqlTokenizer Tokenizer = SqlTokenizers.For(DatabaseDialect.PostgreSql);
    private string _small = "";
    private string _large = "";

    /// <summary>Generates both scripts once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _small = Generate(200);
        _large = Generate(100_000);
    }

    /// <summary>Tokenizes the 200-line script.</summary>
    [Benchmark]
    public int Tokenize200Lines() => Tokenizer.Tokenize(_small).Count;

    /// <summary>Tokenizes the 100,000-line script.</summary>
    [Benchmark]
    public int Tokenize100000Lines() => Tokenizer.Tokenize(_large).Count;

    private static string Generate(int lines)
    {
        var sb = new StringBuilder();
        var written = 0;
        for (var i = 0; written < lines; i++)
        {
            switch (i % 4)
            {
                case 0:
                    sb.Append("SELECT o.id, c.name, SUM(l.qty * l.price) AS total\n");
                    sb.Append("FROM orders o\n");
                    sb.Append("JOIN customers c ON c.id = o.customer_id\n");
                    sb.Append("LEFT JOIN order_lines l ON l.order_id = o.id AND l.qty > 0\n");
                    sb.Append("WHERE o.created_at >= '2026-01-01' AND c.name LIKE 'A%'\n");
                    sb.Append("GROUP BY o.id, c.name;\n");
                    written += 6;
                    break;
                case 1:
                    sb.Append("INSERT INTO customers (id, name, note) VALUES (").Append(i).Append(", 'it''s ").Append(i).Append("', E'line\n');\n");
                    written++;
                    break;
                case 2:
                    sb.Append("-- a line comment about row ").Append(i).Append('\n');
                    sb.Append("/* a block\n   comment */\n");
                    written += 3;
                    break;
                default:
                    sb.Append("UPDATE orders SET status = 'done', total = total * 1.2 WHERE id = ").Append(i).Append(";\n");
                    written++;
                    break;
            }
        }

        return sb.ToString();
    }
}
