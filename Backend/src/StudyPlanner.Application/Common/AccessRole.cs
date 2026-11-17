namespace StudyPlanner.Application.Common;

/// <summary>
/// Rola użytkownika w udostępnianym zasobie (lista, harmonogram). Brak dostępu – także nieprzyjęte zaproszenie –
/// to brak zasobu (404); rola bez potrzebnego uprawnienia to odmowa (403).
/// W JSON: "viewer", "editor", "owner".
/// </summary>
public enum AccessRole
{
    Viewer,
    Editor,
    Owner,
}

/// <summary>Co można zrobić z udostępnianym zasobem. Które role mają które uprawnienia, ustala <see cref="AccessRoleExtensions.Can"/>.</summary>
public enum Permission
{
    /// <summary>Odczyt zasobu i jego zawartości (punkty listy, zadania, tagi zadań).</summary>
    View,

    /// <summary>Zmiany zawartości: dodawanie, edycja, zaznaczanie, przestawianie i usuwanie punktów albo zadań, przypisywanie tagów.</summary>
    EditContent,

    /// <summary>Zmiana nazwy (i dat) zasobu, jego usunięcie oraz zarządzanie osobami z dostępem.</summary>
    Manage,
}

public static class AccessRoleExtensions
{
    /// <summary>
    /// Jedyne miejsce, które przypisuje uprawnienia do ról.
    /// Wyjątek poza macierzą: każda osoba z dostępem może sama zrezygnować z dostępu.
    /// </summary>
    public static bool Can(this AccessRole role, Permission permission) => permission switch
    {
        Permission.View => true,
        Permission.EditContent => role is AccessRole.Editor or AccessRole.Owner,
        Permission.Manage => role is AccessRole.Owner,
        _ => false,
    };
}
