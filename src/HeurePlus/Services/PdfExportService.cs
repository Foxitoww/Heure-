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
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor("#1F2430"));

                page.Header().Column(header =>
                {
                    header.Item().Text("Heure+").FontSize(20).Bold().FontColor("#4F46E5");
                    header.Item().Text($"Rapport — {monthName}").FontSize(12).FontColor("#6B7280");
                });

                page.Content().PaddingVertical(14).Column(content =>
                {
                    content.Spacing(16);

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

                    // --- Salaire estimé ---
                    content.Item().Text("Salaire estimé").FontSize(12).Bold();
                    content.Item().Border(1).BorderColor("#E2E5EC").Padding(10).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });

                        Line(table, "Heures normales",
                            $"{Fmt.H(stats.Salary.NormalHours)}  =  {Money(stats.Salary.NormalPay, salary)}");
                        Line(table, $"Heures sup. (x{salary.OvertimeMultiplier.ToString("0.##", Fmt.Fr)})",
                            $"{Fmt.H(stats.Salary.OvertimeHours)}  =  {Money(stats.Salary.OvertimePay, salary)}");
                        Line(table, "Brut estimé", Money(stats.Salary.Gross, salary), bold: true);
                        if (salary.ApplyEndOfMissionBonus)
                            Line(table, $"IFM ({(salary.EndOfMissionRate * 100).ToString("0.##", Fmt.Fr)} %)",
                                Money(stats.Salary.EndOfMissionBonus, salary));
                        if (salary.ApplyPaidLeaveBonus)
                            Line(table, $"Congés payés ({(salary.PaidLeaveRate * 100).ToString("0.##", Fmt.Fr)} %)",
                                Money(stats.Salary.PaidLeaveBonus, salary));
                        Line(table, "Total estimé", Money(stats.Salary.Total, salary), bold: true);
                    });

                    // --- Journal ---
                    content.Item().Text("Journal des saisies").FontSize(12).Bold();
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70);   // date
                            c.RelativeColumn(2);    // statut
                            c.ConstantColumn(42);   // début
                            c.ConstantColumn(42);   // fin
                            c.ConstantColumn(52);   // h norm
                            c.ConstantColumn(48);   // h sup
                            c.RelativeColumn(3);    // note
                        });

                        table.Header(h =>
                        {
                            foreach (var title in new[] { "Date", "Statut", "Début", "Fin", "H. norm.", "H. sup.", "Note" })
                                h.Cell().Background("#EEF0F6").Padding(4).Text(title).Bold();
                        });

                        foreach (var e in ordered)
                        {
                            Cell(table, e.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                            Cell(table, e.Status.Label());
                            Cell(table, e.StartTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "—");
                            Cell(table, e.EndTime?.ToString("HH\\:mm", CultureInfo.InvariantCulture) ?? "—");
                            Cell(table, e.NormalHours > 0 ? Fmt.H(e.NormalHours) : "—");
                            Cell(table, e.OvertimeHours > 0 ? Fmt.H(e.OvertimeHours) : "—");
                            Cell(table, string.IsNullOrWhiteSpace(e.Note) ? "" : e.Note);
                        }

                        if (ordered.Count == 0)
                            table.Cell().ColumnSpan(7).Padding(6).Text("Aucune saisie ce mois-ci.").FontColor("#6B7280");
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Estimation indicative — ne constitue pas un bulletin de paie. ").FontColor("#6B7280");
                    t.Span("Généré par Heure+ le " +
                           System.DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))
                        .FontColor("#9AA1AE");
                });
            });
        }).GeneratePdf(path);
    }

    private static void Kpi(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor("#E2E5EC").Padding(8).Column(col =>
        {
            col.Item().Text(value).FontSize(14).Bold();
            col.Item().Text(label).FontSize(8).FontColor("#6B7280");
        });
    }

    private static void Line(TableDescriptor table, string label, string value, bool bold = false)
    {
        var l = table.Cell().PaddingVertical(2).Text(label);
        var v = table.Cell().PaddingVertical(2).AlignRight().Text(value);
        if (bold) { l.Bold(); v.Bold(); }
    }

    private static void Cell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(0.5f).BorderColor("#E2E5EC").Padding(4).Text(text);

    private static string Money(double amount, SalarySettings s) => Fmt.Money(amount, s.Currency);
}
