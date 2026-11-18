using Squill.Core.Security;
using Xunit;

namespace Squill.Core.Tests.Security;

public sealed class PrivilegeModelTests
{
    private static readonly PrivilegeObjectKey Key = new("public", "orders", ObjectKind.Table);

    [Fact]
    public void Toggle_None_AddsGrant_ThenCancels()
    {
        var set = new PrivilegeChangeSet("bob");
        Assert.Equal(ToggleResult.Added, set.Toggle(Key, PrivilegeKind.Select, PrivilegeState.None));
        Assert.Equal(PrivilegeChangeKind.Grant, set.Changes[0].Change);
        Assert.Equal("bob", set.Changes[0].Role);
        Assert.Equal(ToggleResult.Removed, set.Toggle(Key, PrivilegeKind.Select, PrivilegeState.None));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Toggle_Granted_AddsRevoke_ThenCancels()
    {
        var set = new PrivilegeChangeSet("bob");
        set.Toggle(Key, PrivilegeKind.Delete, PrivilegeState.Granted);
        Assert.Equal(PrivilegeChangeKind.Revoke, set.Changes[0].Change);
        Assert.Equal(ToggleResult.Removed, set.Toggle(Key, PrivilegeKind.Delete, PrivilegeState.Granted));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Toggle_Inherited_DoesNothing()
    {
        var set = new PrivilegeChangeSet("bob");
        Assert.Equal(ToggleResult.InheritedCannotRevoke, set.Toggle(Key, PrivilegeKind.Select, PrivilegeState.Inherited("readers")));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void ToggleRow_GrantsMissing_ThenRevokesAll_SkippingInherited()
    {
        var current = new Dictionary<PrivilegeKind, PrivilegeState>
        {
            [PrivilegeKind.Select] = PrivilegeState.Inherited("readers"),
            [PrivilegeKind.Insert] = PrivilegeState.Granted,
        };
        var set = new PrivilegeChangeSet("bob");
        set.ToggleRow(Key, current);
        Assert.Equal(5, set.Count);
        Assert.All(set.Changes, c => Assert.Equal(PrivilegeChangeKind.Grant, c.Change));
        Assert.DoesNotContain(set.Changes, c => c.Privilege is PrivilegeKind.Select or PrivilegeKind.Insert);

        set.ToggleRow(Key, current);
        Assert.Equal(1, set.Count);
        Assert.Equal(PrivilegeChangeKind.Revoke, set.Changes[0].Change);
        Assert.Equal(PrivilegeKind.Insert, set.Changes[0].Privilege);
    }

    [Fact]
    public void RoleInfo_IsGroup_SplitsGroupsAndUsers()
    {
        Assert.True(new RoleInfo("g", false, false, true, [], []).IsGroup);
        Assert.True(new RoleInfo("g", true, false, true, [], ["u"]).IsGroup);
        Assert.False(new RoleInfo("u", true, false, true, ["g"], []).IsGroup);
    }

    [Fact]
    public void ObjectPrivileges_EqualByValue()
    {
        var a = new ObjectPrivileges("s", "t", ObjectKind.View, new Dictionary<PrivilegeKind, PrivilegeState> { [PrivilegeKind.Select] = PrivilegeState.Granted });
        var b = new ObjectPrivileges("s", "t", ObjectKind.View, new Dictionary<PrivilegeKind, PrivilegeState> { [PrivilegeKind.Select] = PrivilegeState.Granted });
        Assert.Equal(a, b);
        Assert.Equal(PrivilegeState.None, a.StateOf(PrivilegeKind.Trigger));
    }
}
