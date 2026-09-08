using System;
using System.Collections.Generic;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Calcul estimatif du salaire à partir des saisies et des réglages.</summary>
public static class SalaryCalculator
{
    /// <summary>Taux effectif d'une saisie (taux spécifique s'il est renseigné).</summary>
    public static double RateOf(DayEntry entry, SalarySettings settings) =>
        entry.HourlyRateOverride is > 0 ? entry.HourlyRateOverride!.Value : settings.HourlyRate;

    /// <summary>
    /// Rémunération d'un jour. Les heures sup. positives sont majorées ;
    /// les heures « retirées » (valeur négative) sont décomptées au taux normal.
    /// </summary>
    public static double DayPay(DayEntry entry, SalarySettings settings)
    {
        if (!entry.Status.IsWorking()) return 0;

        double rate = RateOf(entry, settings);
        double normalPay = entry.NormalHours * rate;
        double overtimePay = entry.OvertimeHours >= 0
            ? entry.OvertimeHours * rate * settings.OvertimeMultiplier
            : entry.OvertimeHours * rate;

        return normalPay + overtimePay;
    }

    public static SalaryResult Estimate(IEnumerable<DayEntry> entries, SalarySettings settings)
    {
        double normalHours = 0, overtimeHours = 0, normalPay = 0, overtimePay = 0;

        foreach (var entry in entries)
        {
            // Congés / repos ne produisent pas de rémunération dans cette estimation.
            if (!entry.Status.IsWorking()) continue;

            double rate = RateOf(entry, settings);

            normalHours += entry.NormalHours;
            overtimeHours += entry.OvertimeHours;
            normalPay += entry.NormalHours * rate;
            overtimePay += entry.OvertimeHours >= 0
                ? entry.OvertimeHours * rate * settings.OvertimeMultiplier
                : entry.OvertimeHours * rate;
        }

        double gross = normalPay + overtimePay;
        double ifm = settings.ApplyEndOfMissionBonus ? gross * settings.EndOfMissionRate : 0;
        double icp = settings.ApplyPaidLeaveBonus ? (gross + ifm) * settings.PaidLeaveRate : 0;

        return new SalaryResult
        {
            NormalHours = normalHours,
            OvertimeHours = overtimeHours,
            NormalPay = normalPay,
            OvertimePay = overtimePay,
            EndOfMissionBonus = ifm,
            PaidLeaveBonus = icp
        };
    }
}
