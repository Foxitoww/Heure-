using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Construit les agrégats mensuels (tableau de bord, exports).</summary>
public static class StatsService
{
    public static MonthStats Month(int year, int month, IReadOnlyList<DayEntry> monthEntries,
        SalarySettings salary, IEnumerable<AppliedPrime>? primes = null)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var stats = new MonthStats
        {
            Year = year,
            Month = month,
            HoursByDay = new double[daysInMonth]
        };

        foreach (var entry in monthEntries)
        {
            if (entry.Date.Year != year || entry.Date.Month != month) continue;

            stats.NormalHours += entry.NormalHours;
            stats.OvertimeHours += entry.OvertimeHours;
            stats.HoursByDay[entry.Date.Day - 1] = entry.TotalHours;

            switch (entry.Status)
            {
                case DayStatus.Travail:
                case DayStatus.HeuresSup:
                    stats.WorkedDays++;
                    break;
                case DayStatus.Conge:
                    stats.LeaveDays++;
                    break;
                case DayStatus.Repos:
                    stats.RestDays++;
                    break;
            }

            if (entry.OvertimeHours > 0) stats.OvertimeDays++;
        }

        // Regroupement par semaine ISO.
        var weekBuckets = new SortedDictionary<int, double>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            int week = ISOWeek.GetWeekOfYear(date);
            weekBuckets.TryGetValue(week, out double current);
            weekBuckets[week] = current + stats.HoursByDay[day - 1];
        }
        stats.HoursByWeek = weekBuckets.Select(kv => ($"S{kv.Key}", kv.Value)).ToList();

        var first = new DateOnly(year, month, 1);
        stats.Salary = SalaryCalculator.Estimate(monthEntries, salary, primes,
            (first, first.AddMonths(1).AddDays(-1)));
        return stats;
    }
}
