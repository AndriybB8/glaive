using StudyPlanner.Application.Common;

namespace StudyPlanner.UnitTests.Common;

public class AccessRoleTests
{
    [Theory]
    [InlineData(AccessRole.Viewer, Permission.View, true)]
    [InlineData(AccessRole.Viewer, Permission.EditContent, false)]
    [InlineData(AccessRole.Viewer, Permission.Manage, false)]
    [InlineData(AccessRole.Editor, Permission.View, true)]
    [InlineData(AccessRole.Editor, Permission.EditContent, true)]
    [InlineData(AccessRole.Editor, Permission.Manage, false)]
    [InlineData(AccessRole.Owner, Permission.View, true)]
    [InlineData(AccessRole.Owner, Permission.EditContent, true)]
    [InlineData(AccessRole.Owner, Permission.Manage, true)]
    public void Can_FollowsRolePermissionMatrix(AccessRole role, Permission permission, bool expected)
    {
        Assert.Equal(expected, role.Can(permission));
    }

    [Fact]
    public void Can_UnknownPermission_IsDenied()
    {
        Assert.False(AccessRole.Owner.Can((Permission)999));
    }
}
