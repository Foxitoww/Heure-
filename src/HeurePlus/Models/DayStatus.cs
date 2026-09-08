namespace HeurePlus.Models;

/// <summary>Statut d'une journée dans le calendrier.</summary>
public enum DayStatus
{
    Travail = 0,
    HeuresSup = 1,
    Conge = 2,
    Repos = 3
}

public static class DayStatusExtensions
{
    public static string Label(this DayStatus s) => s switch
    {
        DayStatus.Travail => "Travail",
        DayStatus.HeuresSup => "Heures sup.",
        DayStatus.Conge => "Congé",
        DayStatus.Repos => "Repos",
        _ => s.ToString()
    };

    /// <summary>La journée est-elle décomptée comme travaillée ?</summary>
    public static bool IsWorking(this DayStatus s) => s is DayStatus.Travail or DayStatus.HeuresSup;
}
