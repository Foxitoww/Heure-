using System;
using System.Collections.Generic;
using System.Linq;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Jours fériés français (métropole).</summary>
public static class FrenchHolidays
{
    /// <summary>Dimanche de Pâques (algorithme grégorien anonyme).</summary>
    public static DateOnly EasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100, c = year % 100;
        int d = b / 4, e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    /// <summary>Les 11 jours fériés nationaux d'une année (date + libellé).</summary>
    public static IReadOnlyList<(DateOnly Date, string Name)> ForYear(int year)
    {
        var easter = EasterSunday(year);
        return new List<(DateOnly, string)>
        {
            (new DateOnly(year, 1, 1),   "Jour de l'An"),
            (easter.AddDays(1),          "Lundi de Pâques"),
            (new DateOnly(year, 5, 1),   "Fête du Travail"),
            (new DateOnly(year, 5, 8),   "Victoire 1945"),
            (easter.AddDays(39),         "Ascension"),
            (easter.AddDays(50),         "Lundi de Pentecôte"),
            (new DateOnly(year, 7, 14),  "Fête nationale"),
            (new DateOnly(year, 8, 15),  "Assomption"),
            (new DateOnly(year, 11, 1),  "Toussaint"),
            (new DateOnly(year, 11, 11), "Armistice 1918"),
            (new DateOnly(year, 12, 25), "Noël"),
        };
    }

    private static readonly Dictionary<int, Dictionary<DateOnly, string>> _cache = new();

    private static Dictionary<DateOnly, string> Map(int year)
    {
        if (!_cache.TryGetValue(year, out var map))
        {
            map = ForYear(year).ToDictionary(h => h.Date, h => h.Name);
            _cache[year] = map;
        }
        return map;
    }

    public static string? NameOf(DateOnly date) =>
        Map(date.Year).TryGetValue(date, out var name) ? name : null;

    public static bool IsHoliday(DateOnly date) => Map(date.Year).ContainsKey(date);

    /// <summary>
    /// Indemnité des jours fériés chômés (métropole) sur une plage : jours fériés
    /// tombant du lundi au vendredi et sans saisie de travail, × heures × taux.
    /// </summary>
    public static (int Count, double Amount) UnworkedHolidayPay(
        DateOnly from, DateOnly to,
        IEnumerable<DayEntry> entries, double hoursPerHoliday, double hourlyRate)
    {
        if (to < from) (from, to) = (to, from);

        var worked = entries
            .Where(e => e.Status.IsWorking())
            .Select(e => e.Date)
            .ToHashSet();

        int count = 0;
        for (int year = from.Year; year <= to.Year; year++)
        {
            foreach (var (date, _) in ForYear(year))
            {
                if (date < from || date > to) continue;
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                if (worked.Contains(date)) continue;
                count++;
            }
        }

        return (count, count * Math.Max(0, hoursPerHoliday) * hourlyRate);
    }
}
