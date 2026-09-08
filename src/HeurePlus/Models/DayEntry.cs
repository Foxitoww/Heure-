using System;

namespace HeurePlus.Models;

/// <summary>Une saisie pour un jour donné (une seule par date).</summary>
public sealed class DayEntry
{
    public long Id { get; set; }

    public DateOnly Date { get; set; }

    public DayStatus Status { get; set; } = DayStatus.Travail;

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int BreakMinutes { get; set; }

    /// <summary>Heures normales (hors heures sup).</summary>
    public double NormalHours { get; set; }

    /// <summary>Heures supplémentaires.</summary>
    public double OvertimeHours { get; set; }

    /// <summary>Taux horaire spécifique à cette mission/journée (sinon on prend le taux général).</summary>
    public double? HourlyRateOverride { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public double TotalHours => NormalHours + OvertimeHours;

    public DayEntry Clone() => (DayEntry)MemberwiseClone();
}
