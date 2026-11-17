using StudyPlanner.Application.Common;
using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

/// <summary>Lista razem z rolą, jaką ma w niej użytkownik.</summary>
public sealed record TodoListWithAccess(TodoList List, AccessRole Access);

public static class TodoListAccessExtensions
{
    public static AccessRole ToAccess(this TodoListMemberRole role) =>
        role == TodoListMemberRole.Editor ? AccessRole.Editor : AccessRole.Viewer;
}
