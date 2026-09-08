namespace HeurePlus.Models;

/// <summary>Paramètres de calcul du salaire (onglet Salaire).</summary>
public sealed class SalarySettings
{
    /// <summary>Taux horaire brut de base.</summary>
    public double HourlyRate { get; set; } = 12.00;

    /// <summary>Coefficient de majoration des heures supplémentaires (ex. 1.25 = +25 %).</summary>
    public double OvertimeMultiplier { get; set; } = 1.25;

    /// <summary>Appliquer l'indemnité de fin de mission (IFM, prime de précarité).</summary>
    public bool ApplyEndOfMissionBonus { get; set; } = true;

    /// <summary>Taux IFM en fraction (0.10 = 10 %).</summary>
    public double EndOfMissionRate { get; set; } = 0.10;

    /// <summary>Appliquer l'indemnité compensatrice de congés payés (ICP).</summary>
    public bool ApplyPaidLeaveBonus { get; set; } = true;

    /// <summary>Taux ICP en fraction (0.10 = 10 %).</summary>
    public double PaidLeaveRate { get; set; } = 0.10;

    /// <summary>Durée hebdomadaire de référence (information / repère).</summary>
    public double WeeklyHours { get; set; } = 35;

    /// <summary>SMIC horaire brut de référence (à revaloriser quand le SMIC change).</summary>
    public double SmicHourly { get; set; } = 11.88;

    /// <summary>Coefficient appliqué au SMIC pour obtenir le taux (1.0 = SMIC, 1.1 = SMIC + 10 %).</summary>
    public double SmicCoefficient { get; set; } = 1.0;

    /// <summary>Payer les jours fériés chômés (français, hors week-end, sans saisie).</summary>
    public bool PayPublicHolidays { get; set; }

    /// <summary>Nombre d'heures rémunérées par jour férié chômé.</summary>
    public double PublicHolidayHours { get; set; } = 7;

    public string Currency { get; set; } = "€";

    public SalarySettings Clone() => (SalarySettings)MemberwiseClone();
}
