using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace StudyPlanner.Domain.Calendar;

/// <summary>
/// Własne wydarzenie użytkownika w kalendarzu. Widzi je i zmienia tylko właściciel – wydarzeń się nie udostępnia.
/// Jedna tabela trzyma trzy rodzaje wierszy (jak w Google Calendar):
/// <list type="bullet">
/// <item>wydarzenie jednorazowe – <see cref="Recurrence"/> = None, bez <see cref="SeriesId"/>;</item>
/// <item>seria – wydarzenie powtarzane; jeden wiersz opisuje wszystkie wystąpienia (od pierwszego do <see cref="RecurrenceUntil"/>);</item>
/// <item>wyjątek – zmienione albo odwołane jedno wystąpienie serii (<see cref="SeriesId"/> + <see cref="OriginalDate"/>).</item>
/// </list>
/// Godziny są w UTC, wydarzenia całodniowe to dni kalendarzowe (DateOnly). Strefa czasowa (IANA, np. „Europe/Warsaw”)
/// pozwala liczyć wystąpienia w czasie lokalnym – zajęcia co tydzień o 10:00 zostają o 10:00 także po zmianie czasu.
/// </summary>
public class CalendarEvent
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int ColorLength = 7;
    public const int TimeZoneMaxLength = 64;

    /// <summary>Kolor, gdy użytkownik go nie wybierze (ten sam co domyślny kolor tagu na frontendzie).</summary>
    public const string DefaultColor = "#2563EB";

    private static readonly Regex HexColor = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public Guid Id { get; private set; }

    /// <summary>Właściciel wydarzenia. W bazie: klucz obcy do tabeli users.</summary>
    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Opcjonalny opis (zwykły tekst). Brak opisu = null, nigdy pusty tekst.</summary>
    public string? Description { get; private set; }

    /// <summary>Kolor w formacie #RRGGBB.</summary>
    public string Color { get; private set; } = DefaultColor;

    public bool IsAllDay { get; private set; }

    /// <summary>Wydarzenie w godzinach: początek (UTC). Null dla wydarzenia całodniowego.</summary>
    public DateTime? StartsAt { get; private set; }

    /// <summary>Wydarzenie w godzinach: koniec (UTC). Null dla wydarzenia całodniowego.</summary>
    public DateTime? EndsAt { get; private set; }

    /// <summary>Wydarzenie całodniowe: pierwszy dzień. Null dla wydarzenia w godzinach.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>Wydarzenie całodniowe: ostatni dzień (włącznie). Null dla wydarzenia w godzinach.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Identyfikator strefy czasowej IANA, w której liczy się wystąpienia serii. Wyjątek ma strefę swojej serii.</summary>
    public string TimeZone { get; private set; } = string.Empty;

    public RecurrenceFrequency Recurrence { get; private set; }

    /// <summary>Ostatni dzień (włącznie, w czasie lokalnym), w którym może wypaść wystąpienie. Null = seria bez końca.</summary>
    public DateOnly? RecurrenceUntil { get; private set; }

    /// <summary>Wyjątek: seria, której wystąpienie zmienia. W bazie: klucz obcy do calendar_events (usuwanie kaskadowe).</summary>
    public Guid? SeriesId { get; private set; }

    /// <summary>Wyjątek: dzień (w czasie lokalnym), w którym wystąpienie wypadałoby według serii.</summary>
    public DateOnly? OriginalDate { get; private set; }

    /// <summary>Wyjątek: wystąpienie odwołane (usunięte tylko to jedno).</summary>
    public bool IsCancelled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Zwiększana przy każdej zmianie (concurrency token w EF Core).</summary>
    public int Version { get; private set; }

    // Wymagany przez EF Core.
    private CalendarEvent()
    {
    }

    /// <summary>Nowe wydarzenie jednorazowe albo nowa seria (gdy <paramref name="recurrence"/> jest inne niż None).</summary>
    public static CalendarEvent Create(
        Guid userId,
        string title,
        string? description,
        string color,
        CalendarEventTime time,
        string timeZone,
        RecurrenceFrequency recurrence,
        DateOnly? recurrenceUntil,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValidDetails(title, description, color);
        var zone = FindTimeZone(timeZone);
        EnsureValidRecurrence(time, zone, recurrence, recurrenceUntil);

        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Description = description,
            Color = color,
            TimeZone = zone.Id,
            Recurrence = recurrence,
            RecurrenceUntil = recurrenceUntil,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
        calendarEvent.SetTime(time);

        return calendarEvent;
    }

    /// <summary>Zmienione jedno wystąpienie serii („tylko to wydarzenie”).</summary>
    public static CalendarEvent CreateException(
        CalendarEvent series,
        DateOnly originalDate,
        string title,
        string? description,
        string color,
        CalendarEventTime time,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);
        EnsureValidDetails(title, description, color);
        series.EnsureCanHaveException(originalDate);

        return NewException(series, originalDate, title, description, color, time, isCancelled: false, utcNow);
    }

    /// <summary>Odwołane jedno wystąpienie serii. Przechowuje jego pierwotny czas, żeby wiersz miał pełny kształt.</summary>
    public static CalendarEvent CreateCancellation(CalendarEvent series, DateOnly originalDate, CalendarEventTime time, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        series.EnsureCanHaveException(originalDate);

        return NewException(series, originalDate, series.Title, series.Description, series.Color, time, isCancelled: true, utcNow);
    }

    /// <summary>Zmiana wydarzenia jednorazowego albo całej serii.</summary>
    public void Update(
        string title,
        string? description,
        string color,
        CalendarEventTime time,
        string timeZone,
        RecurrenceFrequency recurrence,
        DateOnly? recurrenceUntil,
        DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (IsException())
        {
            throw new InvalidOperationException("Zmienione wystąpienie serii zmienia się przez UpdateOccurrence.");
        }

        EnsureValidDetails(title, description, color);
        var zone = FindTimeZone(timeZone);
        EnsureValidRecurrence(time, zone, recurrence, recurrenceUntil);

        Title = title;
        Description = description;
        Color = color;
        TimeZone = zone.Id;
        Recurrence = recurrence;
        RecurrenceUntil = recurrenceUntil;
        SetTime(time);
        Touch(utcNow);
    }

    /// <summary>Zmiana wyjątku – pojedynczego wystąpienia serii. Strefa i brak powtarzania zostają bez zmian.</summary>
    public void UpdateOccurrence(string title, string? description, string color, CalendarEventTime time, DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (!IsException())
        {
            throw new InvalidOperationException("UpdateOccurrence zmienia tylko wystąpienie serii.");
        }

        if (IsCancelled)
        {
            throw new InvalidOperationException("Odwołanego wystąpienia nie można zmienić.");
        }

        EnsureValidDetails(title, description, color);

        Title = title;
        Description = description;
        Color = color;
        SetTime(time);
        Touch(utcNow);
    }

    /// <summary>Odwołuje wystąpienie serii („usuń tylko to wydarzenie”). Ponowne odwołanie niczego nie zmienia.</summary>
    public void Cancel(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (!IsException())
        {
            throw new InvalidOperationException("Odwołać można tylko wystąpienie serii – całe wydarzenie się usuwa.");
        }

        if (IsCancelled)
        {
            return;
        }

        IsCancelled = true;
        Touch(utcNow);
    }

    /// <summary>
    /// Kończy serię dzień przed <paramref name="date"/> („to i następne”: dalsza część staje się nową serią albo znika).
    /// Pierwsze wystąpienie zostaje, więc seria nie może stać się pusta – całą serię zmienia się albo usuwa bezpośrednio.
    /// </summary>
    public void EndRecurrenceBefore(DateOnly date, DateTime utcNow)
    {
        EnsureUtc(utcNow);

        if (!IsRecurring() || IsException())
        {
            throw new InvalidOperationException("Zakończyć można tylko serię wydarzeń.");
        }

        if (date <= LocalStartDate())
        {
            throw new ArgumentException("Serię można zakończyć najwcześniej po pierwszym wystąpieniu.", nameof(date));
        }

        if (RecurrenceUntil is { } until && date > until)
        {
            throw new ArgumentException("Seria kończy się wcześniej niż podana data.", nameof(date));
        }

        RecurrenceUntil = date.AddDays(-1);
        Touch(utcNow);
    }

    public CalendarEventTime GetTime() =>
        IsAllDay
            ? CalendarEventTime.AllDay(StartDate!.Value, EndDate!.Value)
            : CalendarEventTime.Timed(StartsAt!.Value, EndsAt!.Value);

    public TimeZoneInfo GetTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

    /// <summary>Dzień pierwszego wystąpienia w czasie lokalnym wydarzenia.</summary>
    public DateOnly LocalStartDate() => GetTime().StartDateIn(GetTimeZone());

    public bool IsRecurring() => Recurrence != RecurrenceFrequency.None;

    public bool IsException() => SeriesId is not null;

    public static bool IsValidColor(string? color) => color is not null && HexColor.IsMatch(color);

    /// <summary>Czy to znany identyfikator strefy IANA (np. „Europe/Warsaw”, „UTC”). Identyfikatory Windows nie przechodzą.</summary>
    public static bool IsValidTimeZone(string? timeZone) => TryFindTimeZone(timeZone, out _);

    private static CalendarEvent NewException(
        CalendarEvent series,
        DateOnly originalDate,
        string title,
        string? description,
        string color,
        CalendarEventTime time,
        bool isCancelled,
        DateTime utcNow)
    {
        var exception = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            UserId = series.UserId,
            Title = title,
            Description = description,
            Color = color,
            TimeZone = series.TimeZone,
            Recurrence = RecurrenceFrequency.None,
            RecurrenceUntil = null,
            SeriesId = series.Id,
            OriginalDate = originalDate,
            IsCancelled = isCancelled,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Version = 1,
        };
        exception.SetTime(time);

        return exception;
    }

    private void EnsureCanHaveException(DateOnly originalDate)
    {
        if (!IsRecurring() || IsException())
        {
            throw new InvalidOperationException("Pojedyncze wystąpienie można zmienić tylko w serii wydarzeń.");
        }

        if (originalDate < LocalStartDate() || (RecurrenceUntil is { } until && originalDate > until))
        {
            throw new ArgumentException("Data wystąpienia jest poza zakresem serii.", nameof(originalDate));
        }
    }

    private void SetTime(CalendarEventTime time)
    {
        ArgumentNullException.ThrowIfNull(time);

        IsAllDay = time.IsAllDay;
        StartsAt = time.StartsAt;
        EndsAt = time.EndsAt;
        StartDate = time.StartDate;
        EndDate = time.EndDate;
    }

    private void Touch(DateTime utcNow)
    {
        UpdatedAt = utcNow;
        Version++;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Data musi być w UTC.", nameof(value));
        }
    }

    private static void EnsureValidDetails(string title, string? description, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (title.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Tytuł może mieć maksymalnie {TitleMaxLength} znaków.", nameof(title));
        }

        if (description is not null && (description.Length == 0 || description.Length > DescriptionMaxLength))
        {
            throw new ArgumentException($"Opis może mieć od 1 do {DescriptionMaxLength} znaków (brak opisu = null).", nameof(description));
        }

        if (!IsValidColor(color))
        {
            throw new ArgumentException("Kolor musi być w formacie szesnastkowym, np. #2563EB.", nameof(color));
        }
    }

    private static void EnsureValidRecurrence(
        CalendarEventTime time,
        TimeZoneInfo zone,
        RecurrenceFrequency recurrence,
        DateOnly? recurrenceUntil)
    {
        ArgumentNullException.ThrowIfNull(time);

        if (!Enum.IsDefined(recurrence))
        {
            throw new ArgumentOutOfRangeException(nameof(recurrence));
        }

        if (recurrenceUntil is null)
        {
            return;
        }

        if (recurrence == RecurrenceFrequency.None)
        {
            throw new ArgumentException("Datę końca powtarzania można podać tylko dla wydarzenia powtarzanego.", nameof(recurrenceUntil));
        }

        if (recurrenceUntil < time.StartDateIn(zone))
        {
            throw new ArgumentException("Powtarzanie nie może kończyć się przed pierwszym wystąpieniem.", nameof(recurrenceUntil));
        }
    }

    private static TimeZoneInfo FindTimeZone(string timeZone)
    {
        if (!TryFindTimeZone(timeZone, out var zone))
        {
            throw new ArgumentException("Nieznana strefa czasowa – podaj identyfikator IANA, np. Europe/Warsaw.", nameof(timeZone));
        }

        return zone;
    }

    private static bool TryFindTimeZone(string? timeZone, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;

        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Length > TimeZoneMaxLength)
        {
            return false;
        }

        // Windows rozpoznaje też własne nazwy („Central European Standard Time”), ale tylko IANA działa wszędzie.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found) || !found.HasIanaId)
        {
            return false;
        }

        zone = found;
        return true;
    }
}
