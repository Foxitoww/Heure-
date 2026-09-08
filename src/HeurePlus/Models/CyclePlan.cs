using System;
using System.Collections.Generic;

namespace HeurePlus.Models;

/// <summary>Un jour du modèle de cycle (données brutes, sérialisables).</summary>
public sealed class CyclePlanStep
{
    public int Status { get; set; }              // (int)DayStatus
    public string? Start { get; set; }           // "HH:mm" ou null
    public string? End { get; set; }
    public int BreakMinutes { get; set; }
    public double NormalHours { get; set; }
    public double OvertimeHours { get; set; }
}

/// <summary>
/// Un cycle (rotation) enregistré : on peut le rouvrir pour le modifier,
/// ce qui régénère les saisies sur sa plage.
/// </summary>
public sealed class CyclePlan
{
    public long Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IncludeWeekends { get; set; }
    public double? HourlyRateOverride { get; set; }
    public string Note { get; set; } = string.Empty;
    public List<CyclePlanStep> Steps { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string RangeLabel => $"du {StartDate:dd/MM/yyyy} au {EndDate:dd/MM/yyyy}";
}
