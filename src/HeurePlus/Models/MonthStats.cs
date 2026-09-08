using System;
using System.Collections.Generic;

namespace HeurePlus.Models;

/// <summary>Résultat d'une estimation de salaire.</summary>
public sealed class SalaryResult
{
    public double NormalHours { get; init; }
    public double OvertimeHours { get; init; }
    public double NormalPay { get; init; }
    public double OvertimePay { get; init; }
    public double PrimesTotal { get; init; }
    public double EndOfMissionBonus { get; init; }
    public double PaidLeaveBonus { get; init; }

    /// <summary>Détail des primes appliquées (libellé, montant).</summary>
    public List<(string Name, double Amount)> PrimeLines { get; init; } = new();

    public double Gross => NormalPay + OvertimePay + PrimesTotal;
    public double Total => Gross + EndOfMissionBonus + PaidLeaveBonus;
    public double TotalHours => NormalHours + OvertimeHours;
}

/// <summary>Agrégats mensuels pour le tableau de bord et les exports.</summary>
public sealed class MonthStats
{
    public int Year { get; init; }
    public int Month { get; init; }

    public double NormalHours { get; set; }
    public double OvertimeHours { get; set; }
    public double TotalHours => NormalHours + OvertimeHours;

    public int WorkedDays { get; set; }
    public int OvertimeDays { get; set; }
    public int LeaveDays { get; set; }
    public int RestDays { get; set; }

    /// <summary>Heures totales par jour du mois. Index 0 = 1er du mois.</summary>
    public double[] HoursByDay { get; set; } = Array.Empty<double>();

    /// <summary>Heures totales par semaine (libellé + heures).</summary>
    public List<(string Label, double Hours)> HoursByWeek { get; set; } = new();

    public SalaryResult Salary { get; set; } = new();
}
