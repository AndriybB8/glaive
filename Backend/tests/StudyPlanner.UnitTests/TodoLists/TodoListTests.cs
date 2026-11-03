using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.UnitTests.TodoLists;

public class TodoListTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsOwnerUtcDatesAndFirstVersion()
    {
        var list = TodoList.Create(UserId, "Na egzamin z baz", Now);

        Assert.NotEqual(Guid.Empty, list.Id);
        Assert.Equal(UserId, list.UserId);
        Assert.Equal("Na egzamin z baz", list.Title);
        Assert.Equal(Now, list.CreatedAt);
        Assert.Equal(Now, list.UpdatedAt);
        Assert.Equal(1, list.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmptyTitle(string title)
    {
        Assert.ThrowsAny<ArgumentException>(() => TodoList.Create(UserId, title, Now));
    }

    [Fact]
    public void Create_RejectsTooLongTitle()
    {
        var title = new string('a', TodoList.TitleMaxLength + 1);

        Assert.Throws<ArgumentException>(() => TodoList.Create(UserId, title, Now));
    }

    [Fact]
    public void Create_AcceptsTitleOfMaxLength()
    {
        var list = TodoList.Create(UserId, new string('a', TodoList.TitleMaxLength), Now);

        Assert.Equal(TodoList.TitleMaxLength, list.Title.Length);
    }

    [Fact]
    public void Create_RejectsNonUtcDate()
    {
        var local = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => TodoList.Create(UserId, "Zakupy", local));
    }

    [Fact]
    public void Rename_ChangesTitleUpdatedAtAndVersion()
    {
        var list = TodoList.Create(UserId, "Zakupy", Now);
        var later = Now.AddMinutes(5);

        list.Rename("Zakupy przed sesją", later);

        Assert.Equal("Zakupy przed sesją", list.Title);
        Assert.Equal(Now, list.CreatedAt);
        Assert.Equal(later, list.UpdatedAt);
        Assert.Equal(2, list.Version);
    }

    [Fact]
    public void Rename_RejectsEmptyTitleAndKeepsOldOne()
    {
        var list = TodoList.Create(UserId, "Zakupy", Now);

        Assert.ThrowsAny<ArgumentException>(() => list.Rename(" ", Now.AddMinutes(5)));
        Assert.Equal("Zakupy", list.Title);
        Assert.Equal(1, list.Version);
    }
}
