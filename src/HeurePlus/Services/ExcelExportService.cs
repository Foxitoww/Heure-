using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.Services;

/// <summary>Export Excel (.xlsx) : feuille « Journal » + feuille « Synthèse ».</summary>
public static class ExcelExportService
{
    public static void ExportMonth(
        string path,
        int year,
        int month,
        IReadOnlyList<DayEntry> entries,
        MonthStats stats,
        SalarySettings salary)
    {
        string moneyFmt = "#,##0.00 \"" + salary.Currency + "\"";

        using var workbook = new XLWorkbook();

        // ---------- Journal ----------
        var journal = workbook.AddWorksheet("Journal");
        string[] headers =
        {
            "Date", "Jour", "Statut", "Début", "Fin", "Pause (min)",
            "H. normales", "H. sup.", "H. totales", "Taux €/h", "À payer", "Note"
        };
        for (int i = 0; i < headers.Length; i++)
            journal.Cell(1, i + 1).Value = headers[i];
        journal.Row(1).Style.Font.Bold = true;
        journal.SheetView.FreezeRows(1);

        int row = 2;
        foreach (var e in entries.OrderBy(e => e.Date))
        {
            double rate = e.HourlyRateOverride is > 0 ? e.HourlyRateOverride!.Value : salary.HourlyRate;
            bool working = e.Status.IsWorking();
            double pay = working ? e.NormalHours * rate + e.OvertimeHours * rate * salary.OvertimeMultiplier : 0;

            var date = e.Date.ToDateTime(TimeOnly.MinValue);
            journal.Cell(row, 1).Value = date;
            journal.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            journal.Cell(row, 2).Value = Fmt.Fr.TextInfo.ToTitleCase(date.ToString("dddd", Fmt.Fr));
            journal.Cell(row, 3).Value = e.Status.Label();
            journal.Cell(row, 4).Value = e.StartTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "";
            journal.Cell(row, 5).Value = e.EndTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "";
            journal.Cell(row, 6).Value = e.BreakMinutes;
            journal.Cell(row, 7).Value = e.NormalHours;
            journal.Cell(row, 8).Value = e.OvertimeHours;
            journal.Cell(row, 9).Value = e.TotalHours;
            journal.Cell(row, 10).Value = rate;
            journal.Cell(row, 10).Style.NumberFormat.Format = moneyFmt;
            journal.Cell(row, 11).Value = pay;
            journal.Cell(row, 11).Style.NumberFormat.Format = moneyFmt;
            journal.Cell(row, 12).Value = e.Note;
            row++;
        }

        if (row > 2)
        {
            int last = row - 1;
            journal.Cell(row, 6).Value = "Totaux";
            journal.Cell(row, 7).FormulaA1 = $"SUM(G2:G{last})";
            journal.Cell(row, 8).FormulaA1 = $"SUM(H2:H{last})";
            journal.Cell(row, 9).FormulaA1 = $"SUM(I2:I{last})";
            journal.Cell(row, 11).FormulaA1 = $"SUM(K2:K{last})";
            journal.Cell(row, 11).Style.NumberFormat.Format = moneyFmt;
            journal.Range(row, 6, row, 11).Style.Font.Bold = true;
            journal.Range(row, 6, row, 12).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEE, 0xF0, 0xF6);
        }

        journal.Columns().AdjustToContents();

        // ---------- Synthèse ----------
        var summary = workbook.AddWorksheet("Synthèse");
        int r = 1;

        void Section(string title)
        {
            summary.Cell(r, 1).Value = title;
            summary.Cell(r, 1).Style.Font.Bold = true;
            summary.Cell(r, 1).Style.Font.FontSize = 12;
            r++;
        }
        void Kv(string key, XLCellValue value, string? numberFormat = null)
        {
            summary.Cell(r, 1).Value = key;
            summary.Cell(r, 2).Value = value;
            if (numberFormat is not null) summary.Cell(r, 2).Style.NumberFormat.Format = numberFormat;
            r++;
        }

        Section($"Mois : {Fmt.Fr.TextInfo.ToTitleCase(new System.DateTime(year, month, 1).ToString("MMMM yyyy", Fmt.Fr))}");

        // Total à payer, mis en avant en tête de synthèse.
        summary.Cell(r, 1).Value = "TOTAL À PAYER (ESTIMÉ)";
        summary.Cell(r, 2).Value = stats.Salary.Total;
        summary.Cell(r, 2).Style.NumberFormat.Format = moneyFmt;
        summary.Range(r, 1, r, 2).Style.Font.Bold = true;
        summary.Range(r, 1, r, 2).Style.Font.FontSize = 13;
        summary.Range(r, 1, r, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xF1, 0xFB);
        r += 2;

        Section("Heures");
        Kv("Heures normales", stats.NormalHours, "0.00");
        Kv("Heures supplémentaires", stats.OvertimeHours, "0.00");
        Kv("Heures totales", stats.TotalHours, "0.00");
        Kv("Jours travaillés", stats.WorkedDays);
        Kv("Jours avec heures sup.", stats.OvertimeDays);
        Kv("Jours de congé", stats.LeaveDays);
        Kv("Jours de repos", stats.RestDays);
        r++;

        Section("Détail du calcul");
        Kv("Taux horaire", salary.HourlyRate, moneyFmt);
        Kv("Coeff. heures sup.", salary.OvertimeMultiplier, "0.00");
        Kv("Paie heures normales", stats.Salary.NormalPay, moneyFmt);
        Kv("Paie heures sup.", stats.Salary.OvertimePay, moneyFmt);
        Kv("Brut estimé", stats.Salary.Gross, moneyFmt);
        if (salary.ApplyEndOfMissionBonus)
            Kv($"IFM ({(salary.EndOfMissionRate * 100).ToString("0.##", Fmt.Fr)} %)",
                stats.Salary.EndOfMissionBonus, moneyFmt);
        if (salary.ApplyPaidLeaveBonus)
            Kv($"Congés payés ({(salary.PaidLeaveRate * 100).ToString("0.##", Fmt.Fr)} %)",
                stats.Salary.PaidLeaveBonus, moneyFmt);
        Kv("Total à payer (estimé)", stats.Salary.Total, moneyFmt);
        summary.Cell(r - 1, 1).Style.Font.Bold = true;
        summary.Cell(r - 1, 2).Style.Font.Bold = true;
        r++;

        summary.Cell(r, 1).Value = "Estimation indicative — ne constitue pas un bulletin de paie.";
        summary.Cell(r, 1).Style.Font.Italic = true;
        summary.Cell(r, 1).Style.Font.FontColor = XLColor.Gray;

        summary.Columns().AdjustToContents();

        workbook.SaveAs(path);
    }
}
