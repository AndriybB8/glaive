using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoItemTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ListId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_StartsNotDoneWithFirstVersion()
    {
        var item = TodoItem.Create(UserId, ListId, "Powtórzyć normalizację", 3, Now);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(UserId, item.UserId);
        Assert.Equal(ListId, item.ListId);
        Assert.Equal("Powtórzyć normalizację", item.Title);
        Assert.Equal(3, item.Position);
        Assert.False(item.IsDone);
        Assert.Null(item.CompletedAt);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal(Now, item.UpdatedAt);
        Assert.Equal(1, item.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyTitle(string title)
    {
        Assert.ThrowsAny<ArgumentException>(() => TodoItem.Create(UserId, ListId, title, 0, Now));
    }

    [Fact]
    public void Create_RejectsTooLongTitle()
    {
        var title = new string('a', TodoItem.TitleMaxLength + 1);

        Assert.Throws<ArgumentException>(() => TodoItem.Create(UserId, ListId, title, 0, Now));
    }

    [Fact]
    public void Create_RejectsNegativePosition()
    {
        Assert.Throws<ArgumentException>(() => TodoItem.Create(UserId, ListId, "Zadanie", -1, Now));
    }

    [Fact]
    public void Create_RejectsNonUtcDate()
    {
        var local = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => TodoItem.Create(UserId, ListId, "Zadanie", 0, local));
    }

    [Fact]
    public void SetDone_True_SetsCompletedAtAndBumpsVersion()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);
        var later = Now.AddMinutes(5);

        item.SetDone(true, later);

        Assert.True(item.IsDone);
        Assert.Equal(later, item.CompletedAt);
        Assert.Equal(later, item.UpdatedAt);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void SetDone_False_ClearsCompletedAt()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);
        item.SetDone(true, Now.AddMinutes(5));

        item.SetDone(false, Now.AddMinutes(10));

        Assert.False(item.IsDone);
        Assert.Null(item.CompletedAt);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public void SetDone_WithSameValue_ChangesNothing()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);
        var doneAt = Now.AddMinutes(5);
        item.SetDone(true, doneAt);

        item.SetDone(true, Now.AddMinutes(10));

        Assert.Equal(doneAt, item.CompletedAt);
        Assert.Equal(doneAt, item.UpdatedAt);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void Rename_ChangesTitleAndKeepsDoneState()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);
        item.SetDone(true, Now.AddMinutes(5));

        item.Rename("Zadanie poprawione", Now.AddMinutes(10));

        Assert.Equal("Zadanie poprawione", item.Title);
        Assert.True(item.IsDone);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public void MoveTo_ChangesPosition()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);

        item.MoveTo(5, Now.AddMinutes(5));

        Assert.Equal(5, item.Position);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void MoveTo_RejectsNegativePosition()
    {
        var item = TodoItem.Create(UserId, ListId, "Zadanie", 0, Now);

        Assert.Throws<ArgumentException>(() => item.MoveTo(-1, Now.AddMinutes(5)));
        Assert.Equal(0, item.Position);
    }
}
