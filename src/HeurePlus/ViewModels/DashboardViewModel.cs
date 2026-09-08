using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;

namespace HeurePlus.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private const double ChartHeight = 150;

    private readonly DayEntryRepository _entries;
    private readonly SettingsRepository _settingsRepo;
    private readonly AppEvents _events;

    private DateOnly _month;

    public DashboardViewModel(DayEntryRepository entries, SettingsRepository settingsRepo, AppEvents events)
    {
        _entries = entries;
        _settingsRepo = settingsRepo;
        _events = events;

        _month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        PrevMonthCommand = new RelayCommand(_ => Move(-1));
        NextMonthCommand = new RelayCommand(_ => Move(+1));
        TodayCommand = new RelayCommand(_ => { _month = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1); Refresh(); });

        _events.EntriesChanged += Refresh;
        _events.SettingsChanged += Refresh;

        Refresh();
    }

    public RelayCommand PrevMonthCommand { get; }
    public RelayCommand NextMonthCommand { get; }
    public RelayCommand TodayCommand { get; }

    public string MonthLabel => Fmt.Fr.TextInfo.ToTitleCase(
        _month.ToDateTime(default).ToString("MMMM yyyy", Fmt.Fr));

    // KPI
    public string TotalHoursText { get; private set; } = "";
    public string OvertimeHoursText { get; private set; } = "";
    public string WorkedDaysText { get; private set; } = "";
    public string LeaveDaysText { get; private set; } = "";
    public string RestDaysText { get; private set; } = "";
    public string EstimatedSalaryText { get; private set; } = "";
    public string AverageDayText { get; private set; } = "";

    public ObservableCollection<BarViewModel> DayBars { get; } = new();
    public ObservableCollection<BarViewModel> WeekBars { get; } = new();
    public ObservableCollection<LegendItem> StatusBreakdown { get; } = new();

    public bool HasData { get; private set; }

    private void Move(int delta)
    {
        _month = _month.AddMonths(delta);
        Refresh();
    }

    private void Refresh()
    {
        var salary = _settingsRepo.LoadSalary();
        var monthEntries = _entries.GetMonth(_month.Year, _month.Month);
        var stats = StatsService.Month(_month.Year, _month.Month, monthEntries, salary);

        TotalHoursText = Fmt.H(stats.TotalHours);
        OvertimeHoursText = Fmt.H(stats.OvertimeHours);
        WorkedDaysText = stats.WorkedDays.ToString(CultureInfo.InvariantCulture);
        LeaveDaysText = stats.LeaveDays.ToString(CultureInfo.InvariantCulture);
        RestDaysText = stats.RestDays.ToString(CultureInfo.InvariantCulture);
        EstimatedSalaryText = Fmt.Money(stats.Salary.Total, salary.Currency);
        AverageDayText = stats.WorkedDays > 0
            ? Fmt.H(stats.TotalHours / stats.WorkedDays)
            : "—";

        HasData = monthEntries.Count > 0;

        // --- Barres par jour ---
        DayBars.Clear();
        double maxDay = stats.HoursByDay.DefaultIfEmpty(0).Max();
        if (maxDay <= 0) maxDay = 1;
        for (int day = 1; day <= stats.HoursByDay.Length; day++)
        {
            double h = stats.HoursByDay[day - 1];
            DayBars.Add(new BarViewModel(
                day.ToString(CultureInfo.InvariantCulture),
                h,
                Math.Round(h / maxDay * ChartHeight, 1),
                $"{day:00}/{_month.Month:00} — {Fmt.H(h)}"));
        }

        // --- Barres par semaine ---
        WeekBars.Clear();
        double maxWeek = stats.HoursByWeek.Select(w => w.Hours).DefaultIfEmpty(0).Max();
        if (maxWeek <= 0) maxWeek = 1;
        foreach (var (label, hours) in stats.HoursByWeek)
        {
            WeekBars.Add(new BarViewModel(
                label,
                hours,
                Math.Round(hours / maxWeek * ChartHeight, 1),
                $"{label} — {Fmt.H(hours)}"));
        }

        // --- Répartition des jours ---
        StatusBreakdown.Clear();
        StatusBreakdown.Add(new LegendItem("Jours travaillés", stats.WorkedDays, nameof(DayStatus.Travail)));
        StatusBreakdown.Add(new LegendItem("Jours avec heures sup.", stats.OvertimeDays, nameof(DayStatus.HeuresSup)));
        StatusBreakdown.Add(new LegendItem("Jours de congé", stats.LeaveDays, nameof(DayStatus.Conge)));
        StatusBreakdown.Add(new LegendItem("Jours de repos", stats.RestDays, nameof(DayStatus.Repos)));

        RaiseAll();
    }
}
