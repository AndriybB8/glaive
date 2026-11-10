using StudyPlanner.Domain.TodoLists;

namespace StudyPlanner.Application.TodoLists;

/// <summary>Co użytkownik może zrobić z listą. Brak dostępu (także nieprzyjęte zaproszenie) to brak listy – 404.</summary>
public enum TodoListAccess
{
    Viewer,
    Editor,
    Owner,
}

/// <summary>Lista razem z dostępem, jaki ma do niej użytkownik.</summary>
public sealed record TodoListWithAccess(TodoList List, TodoListAccess Access);

public static class TodoListAccessExtensions
{
    /// <summary>Dodawanie, edycja, zaznaczanie, przestawianie i usuwanie punktów.</summary>
    public static bool CanEditItems(this TodoListAccess access) => access is TodoListAccess.Editor or TodoListAccess.Owner;

    /// <summary>Zmiana nazwy, usunięcie listy i zarządzanie osobami z dostępem.</summary>
    public static bool CanManageList(this TodoListAccess access) => access is TodoListAccess.Owner;

    public static TodoListAccess ToAccess(this TodoListMemberRole role) =>
        role == TodoListMemberRole.Editor ? TodoListAccess.Editor : TodoListAccess.Viewer;
}
