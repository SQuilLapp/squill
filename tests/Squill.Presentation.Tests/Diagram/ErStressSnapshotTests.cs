using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

/// <summary>Runs the ER pipeline on the schemas the four real engines returned for the er_stress group (JSON written by the Docker-gated driver tests).</summary>
public sealed class ErStressSnapshotTests
{
    public static TheoryData<DatabaseDialect> Dialects() =>
        new(DatabaseDialect.Sqlite, DatabaseDialect.PostgreSql, DatabaseDialect.MySql, DatabaseDialect.SqlServer);

    [Theory]
    [MemberData(nameof(Dialects))]
    public void Snapshot_YieldsAValidDiagram(DatabaseDialect dialect) =>
        ErStressScenario.Verify(ErStressScenario.Load(Path.Combine(AppContext.BaseDirectory, "Diagram", "Snapshots", $"er-{dialect}.json")), dialect);
}
