using Squill.Core.Security;
using Squill.Sql.Security;
using Xunit;

namespace Squill.Sql.Tests.Security;

public sealed class PostgresPrivilegeScriptTests
{
    private static PrivilegeObjectKey Key(string schema, string name) => new(schema, name, ObjectKind.Table);

    private static PrivilegeChangeSet Set(string role, params (string Schema, string Name, PrivilegeKind Kind, bool Granted)[] cells)
    {
        var set = new PrivilegeChangeSet(role);
        foreach (var c in cells)
        {
            set.Toggle(Key(c.Schema, c.Name), c.Kind, c.Granted ? PrivilegeState.Granted : PrivilegeState.None);
        }

        return set;
    }

    [Fact]
    public void Build_OneGrantAndOneRevoke_GoldenText()
    {
        var set = Set("analyste", ("public", "stocks", PrivilegeKind.Select, false), ("analytics", "ventes_jour", PrivilegeKind.Delete, true));
        var script = PostgresPrivilegeScript.Build("analyste", set, "prod");

        Assert.Equal(
            "-- Rôle analyste . prod" + Environment.NewLine
            + "GRANT SELECT ON public.stocks TO analyste;" + Environment.NewLine
            + "REVOKE DELETE ON analytics.ventes_jour FROM analyste;",
            script.Text);
        Assert.Equal(["GRANT SELECT ON public.stocks TO analyste;", "REVOKE DELETE ON analytics.ventes_jour FROM analyste;"], script.Statements);
    }

    [Fact]
    public void Build_KindsOfOneObject_AreMerged()
    {
        var set = Set("r", ("public", "t", PrivilegeKind.Insert, false), ("public", "t", PrivilegeKind.Select, false));

        Assert.Equal(["GRANT SELECT, INSERT ON public.t TO r;"], PostgresPrivilegeScript.Build("r", set, "c").Statements);
    }

    [Fact]
    public void Build_MixedCaseRoleAndDashedSchema_AreQuoted()
    {
        var set = Set("Analyste", ("my-schema", "t", PrivilegeKind.Select, false));

        Assert.Equal(["GRANT SELECT ON \"my-schema\".t TO \"Analyste\";"], PostgresPrivilegeScript.Build("Analyste", set, "c").Statements);
    }

    [Fact]
    public void Build_Ordering_GrantsThenRevokesObjectsByName()
    {
        var set = Set("r", ("b", "t", PrivilegeKind.Delete, true), ("b", "a", PrivilegeKind.Select, false), ("a", "z", PrivilegeKind.Select, false), ("a", "y", PrivilegeKind.Update, true));

        Assert.Equal(
            ["GRANT SELECT ON a.z TO r;", "GRANT SELECT ON b.a TO r;", "REVOKE UPDATE ON a.y FROM r;", "REVOKE DELETE ON b.t FROM r;"],
            PostgresPrivilegeScript.Build("r", set, "c").Statements);
    }

    [Fact]
    public void Build_EmptySet_EmptyText()
    {
        var script = PostgresPrivilegeScript.Build("r", new PrivilegeChangeSet("r"), "c");

        Assert.Equal(string.Empty, script.Text);
        Assert.Empty(script.Statements);
    }

    [Fact]
    public void Build_SameChangesInAnyOrder_SameText()
    {
        var a = Set("r", ("s", "t1", PrivilegeKind.Select, false), ("s", "t2", PrivilegeKind.Delete, true));
        var b = Set("r", ("s", "t2", PrivilegeKind.Delete, true), ("s", "t1", PrivilegeKind.Select, false));

        Assert.Equal(PostgresPrivilegeScript.Build("r", a, "c").Text, PostgresPrivilegeScript.Build("r", b, "c").Text);
    }
}
