using Squill.Core.Editing;
using Xunit;

namespace Squill.Core.Tests;

public sealed class ColumnEditabilityTests
{
    [Theory]
    [InlineData("int", false, ReadOnlyReason.None)]
    [InlineData("integer[]", false, ReadOnlyReason.None)]
    [InlineData("text[]", false, ReadOnlyReason.None)]
    [InlineData("uuid[]", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("int4range", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("sql_variant", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("hierarchyid", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("geography", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("int", true, ReadOnlyReason.Generated)]
    [InlineData("NUMBER(10,2)", false, ReadOnlyReason.None)]
    [InlineData("TIMESTAMP(6) WITH TIME ZONE", false, ReadOnlyReason.None)]
    [InlineData("CLOB", false, ReadOnlyReason.None)]
    [InlineData("LONG", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("XMLTYPE", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("INTERVAL DAY(2) TO SECOND(6)", false, ReadOnlyReason.UnsupportedType)]
    [InlineData("TIMESTAMP(6) WITH LOCAL TIME ZONE", false, ReadOnlyReason.UnsupportedType)]
    public void ReasonFollowsTypeAndGeneratedFlag(string type, bool generated, ReadOnlyReason expected) =>
        Assert.Equal(expected, ColumnEditability.Of(new ColumnDefinition("c", type, true, false, null, false, generated)));
}
