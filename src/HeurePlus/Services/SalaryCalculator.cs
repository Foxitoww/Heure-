using System;
using System.Collections.Generic;
using System.Linq;
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

    public static SalaryResult Estimate(
        IEnumerable<DayEntry> entries,
        SalarySettings settings,
        IEnumerable<AppliedPrime>? primes = null,
        (DateOnly From, DateOnly To)? scope = null)
    {
        var entryList = entries as IReadOnlyList<DayEntry> ?? entries.ToList();
        entries = entryList;

        double normalHours = 0, overtimeHours = 0, normalPay = 0, overtimePay = 0;
        int workedDays = 0;

        foreach (var entry in entries)
        {
            // Congés / repos ne produisent pas de rémunération dans cette estimation.
            if (!entry.Status.IsWorking()) continue;

            workedDays++;
            double rate = RateOf(entry, settings);

            normalHours += entry.NormalHours;
            overtimeHours += entry.OvertimeHours;
            normalPay += entry.NormalHours * rate;
            overtimePay += entry.OvertimeHours >= 0
                ? entry.OvertimeHours * rate * settings.OvertimeMultiplier
                : entry.OvertimeHours * rate;
        }

        double baseGross = normalPay + overtimePay;
        double workedHours = normalHours + Math.Max(0, overtimeHours);

        var primeLines = new List<(string, double)>();
        double primesTotal = 0;
        if (primes is not null)
        {
            foreach (var p in primes)
            {
                if (!p.Enabled || p.Amount == 0) continue;
                double value = p.Unit switch
                {
                    PrimeUnit.Mensuel => p.Amount,
                    PrimeUnit.Fixe => p.Amount,
                    PrimeUnit.ParJourTravaille => p.Amount * workedDays,
                    PrimeUnit.ParHeure => p.Amount * workedHours,
                    PrimeUnit.PourcentBrut => baseGross * p.Amount / 100.0,
                    _ => 0
                };
                if (value == 0) continue;
                primesTotal += value;
                primeLines.Add((p.Name, value));
            }
        }

        // Jours fériés chômés (français) rémunérés
        if (settings.PayPublicHolidays && scope is { } s && settings.PublicHolidayHours > 0)
        {
            var (count, amount) = FrenchHolidays.UnworkedHolidayPay(
                s.From, s.To, entryList, settings.PublicHolidayHours, settings.HourlyRate);
            if (amount > 0)
            {
                primesTotal += amount;
                primeLines.Add(($"Jours fériés chômés ({count})", amount));
            }
        }

        double gross = baseGross + primesTotal;
        double ifm = settings.ApplyEndOfMissionBonus ? gross * settings.EndOfMissionRate : 0;
        double icp = settings.ApplyPaidLeaveBonus ? (gross + ifm) * settings.PaidLeaveRate : 0;

        return new SalaryResult
        {
            NormalHours = normalHours,
            OvertimeHours = overtimeHours,
            NormalPay = normalPay,
            OvertimePay = overtimePay,
            PrimesTotal = primesTotal,
            PrimeLines = primeLines,
            EndOfMissionBonus = ifm,
            PaidLeaveBonus = icp
        };
    }
}
