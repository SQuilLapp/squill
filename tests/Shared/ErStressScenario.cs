using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>JSON context of the committed schema snapshots (test code only).</summary>
[JsonSerializable(typeof(SchemaSnapshot))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class ErSnapshotJsonContext : JsonSerializerContext;

/// <summary>
/// The <c>er_stress</c> group of the sample schemas (40 tables of every shape the ER diagram must survive) and the checks run on what a reader returns for it.
/// Shared by the four driver test projects (which build the group and write the snapshot) and by the Presentation tests (which check the committed snapshots).
/// </summary>
internal static class ErStressScenario
{
    /// <summary>Environment variable naming the folder where the Docker-gated tests write the snapshots; unset means do not write.</summary>
    public const string SnapshotDirVariable = "SQUILL_ER_SNAPSHOT_DIR";

    private const int TableCount = 40;

    private static readonly ErLayoutOptions Options = new(s => s.Length * 7, HeaderHeight: 28, RowHeight: 20, MinWidth: 160);

    /// <summary>Whether the dialect has schemas, so that a cross-schema foreign key exists.</summary>
    public static bool HasSchemas(DatabaseDialect dialect) => dialect is DatabaseDialect.PostgreSql or DatabaseDialect.SqlServer;

    /// <summary>Number of foreign keys the group declares.</summary>
    public static int ExpectedRelations(DatabaseDialect dialect) => (TreeSize(dialect) - 1) + 1 + 2 + 1 + (HasSchemas(dialect) ? 1 : 0);

    private static int TreeSize(DatabaseDialect dialect) => HasSchemas(dialect) ? 32 : 33;

    /// <summary>The statements that (re)create the group, one batch each.</summary>
    /// <param name="dialect">Target engine.</param>
    /// <returns>Statements in execution order.</returns>
    public static IReadOnlyList<string> Statements(DatabaseDialect dialect)
    {
        var schemas = HasSchemas(dialect);
        var integer = dialect == DatabaseDialect.Sqlite ? "integer" : "int";
        string Q(string name) => schemas ? "er_stress." + name : name;
        string Full(string name) => schemas && name == "es_cross" ? "er_stress_b." + name : Q(name);

        List<(string Name, string Sql)> tables = [];
        int fk = 0;
        string Fk(string cols, string parent, string parentCols) => $", constraint fk_es_{++fk} foreign key ({cols}) references {parent} ({parentCols})";
        void Add(string name, string body) => tables.Add((name, $"create table {Full(name)} ({body})"));

        var tree = TreeSize(dialect);
        for (var n = 1; n <= tree; n++)
        {
            var parent = n == 1 ? "" : Fk("parent_id", Q($"es_t{n / 2:00}"), "id");
            Add($"es_t{n:00}", $"id {integer} not null primary key, parent_id {integer} null, v varchar(20) null{parent}");
        }

        Add("es_self", $"id {integer} not null primary key, parent_id {integer} null{Fk("parent_id", Q("es_self"), "id")}");
        Add("es_pair_a", $"id {integer} not null primary key");
        Add("es_pair_b", $"id {integer} not null primary key, x_id {integer} null, y_id {integer} null{Fk("x_id", Q("es_pair_a"), "id")}{Fk("y_id", Q("es_pair_a"), "id")}");
        Add("es_comp_parent", $"ka {integer} not null, kb {integer} not null, primary key (ka, kb)");
        Add("es_comp_child", $"id {integer} not null primary key, ka {integer} null, kb {integer} null{Fk("ka, kb", Q("es_comp_parent"), "ka, kb")}");
        Add("es_wide", $"id {integer} not null primary key, {string.Join(", ", Enumerable.Range(1, 59).Select(i => $"c{i:00} varchar(10) null"))}");
        Add("es_isolated", $"id {integer} not null primary key");
        if (schemas)
            Add("es_cross", $"id {integer} not null primary key, t_id {integer} null{Fk("t_id", Q("es_t01"), "id")}");

        List<string> result = [];
        if (dialect == DatabaseDialect.PostgreSql)
        {
            result.Add("create schema if not exists er_stress");
            result.Add("create schema if not exists er_stress_b");
        }
        else if (dialect == DatabaseDialect.SqlServer)
        {
            result.Add("if schema_id('er_stress') is null exec('create schema er_stress')");
            result.Add("if schema_id('er_stress_b') is null exec('create schema er_stress_b')");
        }

        var cascade = dialect == DatabaseDialect.PostgreSql ? " cascade" : "";
        if (dialect == DatabaseDialect.PostgreSql)
            result.Add($"drop materialized view if exists {Q("es_mv")}");
        result.Add($"drop view if exists {Q("es_v")}");
        foreach (var (name, _) in Enumerable.Reverse(tables))
            result.Add($"drop table if exists {Full(name)}{cascade}");
        result.AddRange(tables.Select(t => t.Sql));
        result.Add($"create view {Q("es_v")} as select id from {Q("es_t01")}");
        if (dialect == DatabaseDialect.PostgreSql)
            result.Add($"create materialized view {Q("es_mv")} as select id from {Q("es_t01")}");
        return result;
    }

    /// <summary>Creates the group on a server, reads the schema back, optionally writes the snapshot, and runs the ER checks on it.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="dialect">Engine of the connection.</param>
    public static async Task RunAsync(IDatabaseConnection connection, DatabaseDialect dialect)
    {
        foreach (var sql in Statements(dialect))
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            do
            {
                await reader.ReadAllAsync(10, CancellationToken.None);
            }
            while (await reader.NextResultAsync(CancellationToken.None));
        }

        var full = await connection.ReadSchemaAsync(CancellationToken.None);
        var snapshot = new SchemaSnapshot(full.DefaultSchema, [.. full.Tables.Where(t => t.Name.StartsWith("es_", StringComparison.OrdinalIgnoreCase))]);
        if (Environment.GetEnvironmentVariable(SnapshotDirVariable) is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, $"er-{dialect}.json"), JsonSerializer.Serialize(snapshot, ErSnapshotJsonContext.Default.SchemaSnapshot));
        }

        Verify(snapshot, dialect);
    }

    /// <summary>Loads a committed snapshot.</summary>
    /// <param name="path">JSON file written by <see cref="RunAsync"/>.</param>
    /// <returns>The snapshot.</returns>
    public static SchemaSnapshot Load(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), ErSnapshotJsonContext.Default.SchemaSnapshot)!;

    /// <summary>Builds the model, layout, routes and SVG of the group and asserts their invariants.</summary>
    /// <param name="snapshot">The group, as read from the engine.</param>
    /// <param name="dialect">Engine it came from.</param>
    public static void Verify(SchemaSnapshot snapshot, DatabaseDialect dialect)
    {
        var views = snapshot.Tables.Where(t => t.Kind.IsViewLike()).Select(t => t.Name).Order().ToList();
        Assert.Equal(dialect == DatabaseDialect.PostgreSql ? ["es_mv", "es_v"] : ["es_v"], views);

        var sw = Stopwatch.StartNew();
        var model = ErModel.Build(snapshot);
        Assert.Equal(TableCount, model.Entities.Count);
        Assert.DoesNotContain(model.Entities, e => e.Kind.IsViewLike());
        Assert.Equal(ExpectedRelations(dialect), model.Relations.Count);
        Assert.Equal(0, model.UnresolvedRelations);
        Assert.Equal(60, model.Entities.Single(e => e.Name == "es_wide").Columns.Count);
        Assert.Contains(model.Entities, e => e.Name == "es_isolated");
        Assert.DoesNotContain(model.Relations, r => model.Find(r.FromKey)!.Name == "es_isolated" || model.Find(r.ToKey)!.Name == "es_isolated");
        Assert.Single(model.Relations, r => r.FromKey == r.ToKey);
        Assert.Equal(2, model.Relations.Count(r => model.Find(r.FromKey)!.Name == "es_pair_b"));
        Assert.Contains(model.Relations, r => r.FromColumns.Count == 2 && r.ToColumns.Count == 2);
        Assert.Equal(HasSchemas(dialect), model.Relations.Any(r => model.Find(r.FromKey)!.Schema != model.Find(r.ToKey)!.Schema));

        var boxes = ErLayout.Compute(model, Options);
        Assert.Equal(TableCount, boxes.Count);
        var list = boxes.Values.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var (a, b) = (list[i], list[j]);
                Assert.False(a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom, $"boxes {i} and {j} overlap");
            }
        }

        var edges = ErRouter.Route(model, boxes, Options);
        Assert.Equal(model.Relations.Count, edges.Count);
        foreach (var e in edges)
        {
            OnBorder(boxes[e.Relation.FromKey], e.FromAnchor);
            OnBorder(boxes[e.Relation.ToKey], e.ToAnchor);
            Assert.Equal(e.FromAnchor, e.Points[0]);
            Assert.Equal(e.ToAnchor, e.Points[^1]);
        }

        Assert.Equal(edges.Count, edges.Select(e => string.Join(';', e.Points)).Distinct().Count());

        var svg = ErSvgExporter.Render(model, boxes, edges, Options, dark: false);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var doc = XDocument.Parse(svg);
        Assert.Equal(2 * TableCount, doc.Descendants(ns + "rect").Count());
        Assert.Equal(edges.Count, doc.Descendants(ns + "polyline").Count());
        foreach (var e in model.Entities)
            Assert.Contains(doc.Descendants(ns + "text"), t => t.Value == e.Name);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"ER pipeline took {sw.Elapsed}");
    }

    private static void OnBorder(ErBox box, ErPoint p)
    {
        Assert.True(p.X == box.X || p.X == box.Right, $"anchor x {p.X} is not on the vertical borders of {box}");
        Assert.True(p.Y >= box.Y && p.Y <= box.Bottom, $"anchor y {p.Y} is outside {box}");
    }
}
