using System;
using System.Collections.Generic;
using System.Linq;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Fenêtre « Ajouter / modifier une journée ou une période ».</summary>
public sealed class EntryEditorViewModel : ObservableObject
{
    private readonly DayEntryRepository _repo;
    private readonly SalarySettings _salary;

    public event Action<bool>? CloseRequested;

    public EntryEditorViewModel(
        DayEntryRepository repo,
        SalarySettings salary,
        DateOnly date,
        DayEntry? existing,
        bool periodMode)
    {
        _repo = repo;
        _salary = salary;

        _date = date.ToDateTime(default);
        _startDate = _date;
        _endDate = _date;
        _isPeriod = periodMode;

        if (existing is not null)
        {
            _status = existing.Status;
            _startText = existing.StartTime?.ToString("HH\\:mm") ?? string.Empty;
            _endText = existing.EndTime?.ToString("HH\\:mm") ?? string.Empty;
            _breakMinutes = existing.BreakMinutes;
            _normalHours = existing.NormalHours;
            _overtimeHours = existing.OvertimeHours;
            _useCustomRate = existing.HourlyRateOverride is > 0;
            _customRate = existing.HourlyRateOverride ?? salary.HourlyRate;
            _note = existing.Note;
            Title = $"Modifier — {date:dd/MM/yyyy}";
        }
        else
        {
            _customRate = salary.HourlyRate;
            Title = periodMode ? "Ajouter une période" : "Ajouter une journée";
        }

        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        ComputeFromScheduleCommand = new RelayCommand(_ => ComputeFromSchedule());
    }

    public string Title { get; }

    public sealed record StatusOption(DayStatus Value, string Label)
    {
        public override string ToString() => Label;
    }

    public IReadOnlyList<StatusOption> StatusOptions { get; } =
        Enum.GetValues<DayStatus>().Select(s => new StatusOption(s, s.Label())).ToArray();

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ComputeFromScheduleCommand { get; }

    private bool _isPeriod;
    public bool IsPeriod
    {
        get => _isPeriod;
        set { if (SetProperty(ref _isPeriod, value)) OnPropertyChanged(nameof(IsSingleDay)); }
    }

    public bool IsSingleDay => !_isPeriod;

    private DateTime _date;
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }

    private DateTime _startDate;
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private DateTime _endDate;
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

    private bool _includeWeekends;
    public bool IncludeWeekends { get => _includeWeekends; set => SetProperty(ref _includeWeekends, value); }

    private bool _overwriteExisting = true;
    public bool OverwriteExisting { get => _overwriteExisting; set => SetProperty(ref _overwriteExisting, value); }

    private DayStatus _status = DayStatus.Travail;
    public DayStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(HoursEnabled));
        }
    }

    public bool HoursEnabled => _status.IsWorking();

    private string _startText = "08:00";
    public string StartText { get => _startText; set => SetProperty(ref _startText, value); }

    private string _endText = "16:00";
    public string EndText { get => _endText; set => SetProperty(ref _endText, value); }

    private int _breakMinutes;
    public int BreakMinutes { get => _breakMinutes; set => SetProperty(ref _breakMinutes, value); }

    private double _normalHours;
    public double NormalHours { get => _normalHours; set => SetProperty(ref _normalHours, value); }

    private double _overtimeHours;
    public double OvertimeHours { get => _overtimeHours; set => SetProperty(ref _overtimeHours, value); }

    private bool _useCustomRate;
    public bool UseCustomRate { get => _useCustomRate; set => SetProperty(ref _useCustomRate, value); }

    private double _customRate;
    public double CustomRate { get => _customRate; set => SetProperty(ref _customRate, value); }

    private string _note = string.Empty;
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    private string? _error;
    public string? Error { get => _error; set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasError)); } }

    public bool HasError => !string.IsNullOrEmpty(_error);

    private void ComputeFromSchedule()
    {
        var start = ParseTime(_startText);
        var end = ParseTime(_endText);
        if (start is null || end is null)
        {
            Error = "Horaires invalides (format attendu : HH:mm).";
            return;
        }

        double span = (end.Value - start.Value).TotalHours;
        if (span < 0) span += 24; // service de nuit
        span -= _breakMinutes / 60.0;
        span = Math.Max(0, Math.Round(span, 2));

        NormalHours = Math.Max(0, span - OvertimeHours);
        Error = null;
    }

    private void Save()
    {
        Error = null;

        var dates = BuildDates();
        if (dates.Count == 0)
        {
            Error = IsPeriod
                ? "La période ne contient aucun jour (vérifiez les dates / les week-ends)."
                : "Date invalide.";
            return;
        }

        TimeOnly? start = ParseTime(_startText);
        TimeOnly? end = ParseTime(_endText);
        if (HoursEnabled && !string.IsNullOrWhiteSpace(_startText) && start is null)
        {
            Error = "Heure de début invalide (format HH:mm).";
            return;
        }
        if (HoursEnabled && !string.IsNullOrWhiteSpace(_endText) && end is null)
        {
            Error = "Heure de fin invalide (format HH:mm).";
            return;
        }

        double normal = HoursEnabled ? Math.Max(0, NormalHours) : 0;
        double overtime = HoursEnabled ? Math.Max(0, OvertimeHours) : 0;
        double? rate = UseCustomRate && CustomRate > 0 ? CustomRate : null;

        var toSave = new List<DayEntry>();
        foreach (var d in dates)
        {
            if (!OverwriteExisting && _repo.Get(d) is not null) continue;

            toSave.Add(new DayEntry
            {
                Date = d,
                Status = _status,
                StartTime = HoursEnabled ? start : null,
                EndTime = HoursEnabled ? end : null,
                BreakMinutes = HoursEnabled ? Math.Max(0, _breakMinutes) : 0,
                NormalHours = normal,
                OvertimeHours = overtime,
                HourlyRateOverride = rate,
                Note = _note?.Trim() ?? string.Empty,
                UpdatedAt = DateTime.Now
            });
        }

        if (toSave.Count == 0)
        {
            Error = "Toutes les dates existent déjà et « écraser » est décoché.";
            return;
        }

        _repo.SaveMany(toSave);
        CloseRequested?.Invoke(true);
    }

    private List<DateOnly> BuildDates()
    {
        var result = new List<DateOnly>();

        if (!IsPeriod)
        {
            result.Add(DateOnly.FromDateTime(_date));
            return result;
        }

        var from = DateOnly.FromDateTime(_startDate.Date);
        var to = DateOnly.FromDateTime(_endDate.Date);
        if (to < from) (from, to) = (to, from);

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            bool weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (weekend && !IncludeWeekends) continue;
            result.Add(d);
        }
        return result;
    }

    private static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace('h', ':').Replace('H', ':');
        if (!text.Contains(':')) text += ":00";
        return TimeOnly.TryParse(text, out var t) ? t : null;
    }
}
