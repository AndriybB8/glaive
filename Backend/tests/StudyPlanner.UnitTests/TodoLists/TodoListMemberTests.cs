using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoListMemberTests
{
    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static TodoListMember Invite(TodoListMemberRole role = TodoListMemberRole.Viewer) =>
        TodoListMember.Invite(ListId, UserId, role, Now);

    [Theory]
    [InlineData(TodoListMemberRole.Viewer)]
    [InlineData(TodoListMemberRole.Editor)]
    public void Invite_SetsListUserRoleAndDateAndIsNotAcceptedYet(TodoListMemberRole role)
    {
        var member = Invite(role);

        Assert.Equal(ListId, member.ListId);
        Assert.Equal(UserId, member.UserId);
        Assert.Equal(role, member.Role);
        Assert.Equal(Now, member.CreatedAt);
        Assert.Null(member.AcceptedAt);
    }

    [Fact]
    public void Invite_RejectsNonUtcDate()
    {
        var local = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => TodoListMember.Invite(ListId, UserId, TodoListMemberRole.Viewer, local));
    }

    [Fact]
    public void Invite_RejectsUnknownRole()
    {
        Assert.Throws<ArgumentException>(() => Invite((TodoListMemberRole)99));
    }

    [Fact]
    public void Accept_SetsAcceptedAtAndKeepsInvitationDate()
    {
        var member = Invite();
        var later = Now.AddHours(1);

        member.Accept(later);

        Assert.Equal(later, member.AcceptedAt);
        Assert.Equal(Now, member.CreatedAt);
    }

    [Fact]
    public void Accept_Twice_KeepsFirstDate()
    {
        var member = Invite();
        var first = Now.AddHours(1);

        member.Accept(first);
        member.Accept(first.AddHours(1));

        Assert.Equal(first, member.AcceptedAt);
    }

    [Fact]
    public void Accept_RejectsNonUtcDate()
    {
        var member = Invite();
        var local = new DateTime(2026, 11, 8, 13, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => member.Accept(local));
        Assert.Null(member.AcceptedAt);
    }

    [Fact]
    public void ChangeRole_ChangesRoleAndKeepsAcceptance()
    {
        var member = Invite();
        var accepted = Now.AddHours(1);
        member.Accept(accepted);

        member.ChangeRole(TodoListMemberRole.Editor);

        Assert.Equal(TodoListMemberRole.Editor, member.Role);
        Assert.Equal(accepted, member.AcceptedAt);
    }

    [Fact]
    public void ChangeRole_RejectsUnknownRoleAndKeepsOldOne()
    {
        var member = Invite();

        Assert.Throws<ArgumentException>(() => member.ChangeRole((TodoListMemberRole)99));
        Assert.Equal(TodoListMemberRole.Viewer, member.Role);
    }
}
