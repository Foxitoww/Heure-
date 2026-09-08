using System.Collections.Generic;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Calcul estimatif du salaire à partir des saisies et des réglages.</summary>
public static class SalaryCalculator
{
    public static SalaryResult Estimate(IEnumerable<DayEntry> entries, SalarySettings settings)
    {
        double normalHours = 0, overtimeHours = 0, normalPay = 0, overtimePay = 0;

        foreach (var entry in entries)
        {
            // Congés / repos ne produisent pas de rémunération dans cette estimation.
            if (!entry.Status.IsWorking()) continue;

            double rate = entry.HourlyRateOverride is > 0
                ? entry.HourlyRateOverride!.Value
                : settings.HourlyRate;

            normalHours += entry.NormalHours;
            overtimeHours += entry.OvertimeHours;
            normalPay += entry.NormalHours * rate;
            overtimePay += entry.OvertimeHours * rate * settings.OvertimeMultiplier;
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
