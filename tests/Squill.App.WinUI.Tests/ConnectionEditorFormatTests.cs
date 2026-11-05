using Squill.App.WinUI.Views;
using Squill.Core;
using Squill.Presentation.Connections;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class ConnectionEditorFormatTests
{
    [Fact]
    public void Engine_tiles_cover_every_dialect_and_add_a_MariaDb_tile()
    {
        Assert.Equal(Enum.GetValues<DatabaseDialect>().Order(), ConnectionEditorFormat.Engines.Select(e => e.Dialect).Distinct().Order());
        Assert.Equal(Enum.GetValues<DatabaseDialect>().Length + 1, ConnectionEditorFormat.Engines.Distinct().Count());
        Assert.Single(ConnectionEditorFormat.Engines, e => e.MariaDb);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "PG", "Postgres", "PostgreSQL")]
    [InlineData(DatabaseDialect.MySql, "My", "Mysql", "MySQL")]
    [InlineData(DatabaseDialect.SqlServer, "MS", "Sqlserver", "SQL Server")]
    [InlineData(DatabaseDialect.Sqlite, "SL", "Sqlite", "SQLite")]
    [InlineData(DatabaseDialect.Oracle, "OR", "Oracle", "Oracle")]
    public void Engine_badge_and_name(DatabaseDialect dialect, string letters, string suffix, string name)
    {
        Assert.Equal((letters, suffix), ConnectionEditorFormat.Badge(dialect));
        Assert.Equal(name, ConnectionEditorFormat.EngineName(dialect));
    }

    [Fact]
    public void Environment_tiles_are_the_four_environments_in_drawn_order()
    {
        Assert.Equal(
            [ConnectionEnvironment.Production, ConnectionEnvironment.Staging, ConnectionEnvironment.Development, ConnectionEnvironment.Local],
            ConnectionEditorFormat.Environments);
        Assert.All(ConnectionEditorFormat.Environments, e => Assert.NotEmpty(TabInfo.StyleSuffix(e)));
    }

    [Fact]
    public void Folders_lists_named_groups_only()
    {
        ConnectionGroup[] groups = [new("Clients", []), new("Interne", []), new(null, [])];
        Assert.Equal(["Clients", "Interne"], ConnectionEditorFormat.Folders(groups));
    }

    [Theory]
    [InlineData("5432", 5432)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void Port_text_round_trips(string text, int? port)
    {
        Assert.Equal(port, ConnectionEditorFormat.ParsePort(text));
        Assert.Equal(port is null ? "" : text, ConnectionEditorFormat.PortText(port));
    }

    [Fact]
    public void Ssh_summary_joins_host_port_and_user() =>
        Assert.Equal("bastion:22 · deploy", ConnectionEditorFormat.SshSummary("bastion", 22, "deploy"));
}
