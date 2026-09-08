using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HeurePlus.Services;

/// <summary>Rapport mensuel PDF (QuestPDF, police embarquée -> aucune dépendance système).</summary>
public static class PdfExportService
{
    private const string Accent = "#0F6CBD";
    private const string AccentSoft = "#E8F1FB";
    private const string Ink = "#1B1B1B";
    private const string Muted = "#5A5A5A";
    private const string Stroke = "#E2E5EC";
    private const string HeaderFill = "#EEF0F6";

    public static void ExportMonth(
        string path,
        int year,
        int month,
        IReadOnlyList<DayEntry> entries,
        MonthStats stats,
        SalarySettings salary)
    {
        var monthName = Fmt.Fr.TextInfo.ToTitleCase(
            new System.DateTime(year, month, 1).ToString("MMMM yyyy", Fmt.Fr));

        var ordered = entries.OrderBy(e => e.Date).ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Text("Heure+").FontSize(20).Bold().FontColor(Accent);
                    header.Item().Text($"Feuille d'heures — {monthName}").FontSize(12).FontColor(Muted);
                });

                page.Content().PaddingVertical(14).Column(content =>
                {
                    content.Spacing(14);

                    // --- Total à payer (mis en avant) ---
                    content.Item().Background(AccentSoft).Border(1).BorderColor(AccentSoft).Padding(14).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("TOTAL À PAYER (ESTIMÉ)").FontSize(8).Bold().FontColor(Accent).LetterSpacing(0.05f);
                            col.Item().Text(Money(stats.Salary.Total, salary)).FontSize(22).Bold().FontColor(Accent);
                        });
                        row.ConstantItem(210).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Brut estimé : {Money(stats.Salary.Gross, salary)}").FontSize(9).FontColor(Muted);
                            if (salary.ApplyEndOfMissionBonus)
                                col.Item().Text($"+ IFM : {Money(stats.Salary.EndOfMissionBonus, salary)}").FontSize(9).FontColor(Muted);
                            if (salary.ApplyPaidLeaveBonus)
                                col.Item().Text($"+ Congés payés : {Money(stats.Salary.PaidLeaveBonus, salary)}").FontSize(9).FontColor(Muted);
                        });
                    });

                    // --- Indicateurs ---
                    content.Item().Row(row =>
                    {
                        row.Spacing(8);
                        Kpi(row, "Heures totales", Fmt.H(stats.TotalHours));
                        Kpi(row, "Heures sup.", Fmt.H(stats.OvertimeHours));
                        Kpi(row, "Jours travaillés", stats.WorkedDays.ToString(CultureInfo.InvariantCulture));
                        Kpi(row, "Congés", stats.LeaveDays.ToString(CultureInfo.InvariantCulture));
                        Kpi(row, "Repos", stats.RestDays.ToString(CultureInfo.InvariantCulture));
                    });

                    // --- Détail du calcul ---
                    content.Item().Text("Détail du calcul").FontSize(12).Bold();
                    content.Item().Border(1).BorderColor(Stroke).Padding(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });

                        Line(table, $"Heures normales — {Fmt.H(stats.Salary.NormalHours)}",
                            Money(stats.Salary.NormalPay, salary));
                        Line(table, $"Heures sup. (x{salary.OvertimeMultiplier.ToString("0.##", Fmt.Fr)}) — {Fmt.H(stats.Salary.OvertimeHours)}",
                            Money(stats.Salary.OvertimePay, salary));
                        Line(table, "Brut estimé", Money(stats.Salary.Gross, salary), bold: true);
                        if (salary.ApplyEndOfMissionBonus)
                            Line(table, $"IFM ({(salary.EndOfMissionRate * 100).ToString("0.##", Fmt.Fr)} %)",
                                Money(stats.Salary.EndOfMissionBonus, salary));
                        if (salary.ApplyPaidLeaveBonus)
                            Line(table, $"Congés payés ({(salary.PaidLeaveRate * 100).ToString("0.##", Fmt.Fr)} %)",
                                Money(stats.Salary.PaidLeaveBonus, salary));
                        Line(table, "Total à payer (estimé)", Money(stats.Salary.Total, salary), bold: true);
                    });

                    // --- Journal ---
                    content.Item().Text("Journal des saisies").FontSize(12).Bold();
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(60);   // date
                            c.RelativeColumn(1.6f);  // statut
                            c.ConstantColumn(38);   // début
                            c.ConstantColumn(38);   // fin
                            c.ConstantColumn(46);   // h norm
                            c.ConstantColumn(42);   // h sup
                            c.ConstantColumn(60);   // à payer
                            c.RelativeColumn(2);    // note
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Date", "Statut", "Début", "Fin", "H. norm.", "H. sup.", "À payer", "Note" })
                                h.Cell().Background(HeaderFill).Padding(4).Text(title).Bold();
                        });

                        double totalNormal = 0, totalOt = 0, totalPay = 0;
                        foreach (var e in ordered)
                        {
                            bool working = e.Status.IsWorking();
                            double pay = SalaryCalculator.DayPay(e, salary);
                            totalNormal += e.NormalHours;
                            totalOt += e.OvertimeHours;
                            totalPay += pay;

                            Cell(table, e.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                            Cell(table, e.Status.Label());
                            Cell(table, e.StartTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "—");
                            Cell(table, e.EndTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "—");
                            Cell(table, e.NormalHours > 0 ? Fmt.H(e.NormalHours) : "—");
                            Cell(table, e.OvertimeHours > 0 ? Fmt.H(e.OvertimeHours) : "—");
                            Cell(table, working ? Money(pay, salary) : "—");
                            Cell(table, string.IsNullOrWhiteSpace(e.Note) ? "" : e.Note);
                        }

                        if (ordered.Count == 0)
                        {
                            table.Cell().ColumnSpan(8).Padding(6).Text("Aucune saisie ce mois-ci.").FontColor(Muted);
                        }
                        else
                        {
                            table.Cell().ColumnSpan(4).Background(HeaderFill).Padding(4).Text("Totaux").Bold();
                            table.Cell().Background(HeaderFill).Padding(4).Text(Fmt.H(totalNormal)).Bold();
                            table.Cell().Background(HeaderFill).Padding(4).Text(Fmt.H(totalOt)).Bold();
                            table.Cell().Background(HeaderFill).Padding(4).Text(Money(totalPay, salary)).Bold();
                            table.Cell().Background(HeaderFill).Padding(4).Text("");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Estimation indicative — ne constitue pas un bulletin de paie. ").FontColor(Muted);
                    t.Span("Généré par Heure+ le " +
                           System.DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                        .FontColor("#9AA1AE");
                });
            });
        }).GeneratePdf(path);
    }

    private static void Kpi(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor(Stroke).Padding(8).Column(col =>
        {
            col.Item().Text(value).FontSize(14).Bold();
            col.Item().Text(label).FontSize(8).FontColor(Muted);
        });
    }

    private static void Line(TableDescriptor table, string label, string value, bool bold = false)
    {
        var l = table.Cell().PaddingVertical(2).Text(label);
        var v = table.Cell().PaddingVertical(2).AlignRight().Text(value);
        if (bold) { l.Bold(); v.Bold(); }
    }

    private static void Cell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor(Stroke).Padding(4).Text(text);

    private static string Money(double amount, SalarySettings s) => Fmt.Money(amount, s.Currency);
}
