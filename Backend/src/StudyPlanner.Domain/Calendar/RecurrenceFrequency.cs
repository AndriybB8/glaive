namespace StudyPlanner.Domain.Calendar;

/// <summary>Jak często powtarza się wydarzenie. Kolejne wystąpienia liczy się w czasie lokalnym wydarzenia.</summary>
public enum RecurrenceFrequency
{
    /// <summary>Wydarzenie jednorazowe.</summary>
    None,

    /// <summary>Codziennie.</summary>
    Daily,

    /// <summary>Co tydzień, w ten sam dzień tygodnia.</summary>
    Weekly,

    /// <summary>Co miesiąc, w ten sam dzień miesiąca. Miesiące bez tego dnia (np. 31.) są pomijane.</summary>
    Monthly,
}
