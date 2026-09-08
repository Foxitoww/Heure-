using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

public enum EntryKind { Day, Period, Cycle }

/// <summary>Fenêtre « Ajouter / modifier une journée, une période ou un cycle ».</summary>
public sealed class EntryEditorViewModel : ObservableObject
{
    private readonly DayEntryRepository _repo;
    private readonly ActivityLogRepository _activityLog;
    private readonly CycleRepository _cycleRepo;
    private readonly SalarySettings _salary;

    private long _editingCycleId;
    private DateOnly _originalCycleStart, _originalCycleEnd;

    public event Action<bool>? CloseRequested;

    public EntryEditorViewModel(
        DayEntryRepository repo,
        ActivityLogRepository activityLog,
        CycleRepository cycleRepo,
        SalarySettings salary,
        DateOnly date,
        DayEntry? existing,
        EntryKind kind,
        CyclePlan? editCycle = null)
    {
        _repo = repo;
        _activityLog = activityLog;
        _cycleRepo = cycleRepo;
        _salary = salary;

        _kind = editCycle is not null ? EntryKind.Cycle : kind;
        _date = date.ToDateTime(default);
        _startDate = _date;
        _endDate = _date.AddDays(13);
        _customRate = salary.HourlyRate;

        // Nouveau cycle : par défaut, toute la plage du mois de la date d'ancrage.
        if (editCycle is null && _kind == EntryKind.Cycle)
        {
            var firstOfMonth = new DateOnly(date.Year, date.Month, 1);
            _startDate = firstOfMonth.ToDateTime(default);
            _endDate = firstOfMonth.AddMonths(1).AddDays(-1).ToDateTime(default);
        }

        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        ComputeFromScheduleCommand = new RelayCommand(_ => ComputeFromSchedule());
        AddCycleStepCommand = new RelayCommand(_ => AddCycleStep());
        RemoveCycleStepCommand = new RelayCommand(p => RemoveCycleStep(p as CycleStepViewModel));
        ApplyPresetCommand = new RelayCommand(p => ApplyPreset(p?.ToString() ?? ""));

        if (editCycle is not null)
        {
            _editingCycleId = editCycle.Id;
            _originalCycleStart = editCycle.StartDate;
            _originalCycleEnd = editCycle.EndDate;
            _startDate = editCycle.StartDate.ToDateTime(default);
            _endDate = editCycle.EndDate.ToDateTime(default);
            _includeWeekends = editCycle.IncludeWeekends;
            _useCustomRate = editCycle.HourlyRateOverride is > 0;
            _customRate = editCycle.HourlyRateOverride ?? salary.HourlyRate;
            _note = editCycle.Note;
            foreach (var s in editCycle.Steps)
                CycleSteps.Add(new CycleStepViewModel(CycleSteps.Count + 1, (DayStatus)s.Status,
                    s.Start ?? string.Empty, s.End ?? string.Empty, s.BreakMinutes, s.NormalHours, s.OvertimeHours));
            Renumber();
            Title = "Modifier le cycle";
        }
        else if (existing is not null)
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
            Title = _kind switch
            {
                EntryKind.Cycle => "Ajouter un cycle",
                EntryKind.Period => "Ajouter une période",
                _ => "Ajouter une journée"
            };
        }

        if (CycleSteps.Count == 0) ApplyPreset("2/2");
    }

    public string Title { get; }
    public bool IsEditingCycle => _editingCycleId > 0;

    public sealed record StatusOption(DayStatus Value, string Label)
    {
        public override string ToString() => Label;
    }

    public IReadOnlyList<StatusOption> StatusOptions { get; } =
        Enum.GetValues<DayStatus>().Select(s => new StatusOption(s, s.Label())).ToArray();

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ComputeFromScheduleCommand { get; }
    public RelayCommand AddCycleStepCommand { get; }
    public RelayCommand RemoveCycleStepCommand { get; }
    public RelayCommand ApplyPresetCommand { get; }

    // ---------- Type de saisie ----------

    private EntryKind _kind;

    public bool IsSingleDay => _kind == EntryKind.Day;
    public bool IsPeriod => _kind == EntryKind.Period;
    public bool IsCycle => _kind == EntryKind.Cycle;
    public bool ShowSchedule => _kind != EntryKind.Cycle;   // horaires simples cachés en mode cycle

    public bool KindDay
    {
        get => _kind == EntryKind.Day;
        set { if (value) SetKind(EntryKind.Day); }
    }
    public bool KindPeriod
    {
        get => _kind == EntryKind.Period;
        set { if (value) SetKind(EntryKind.Period); }
    }
    public bool KindCycle
    {
        get => _kind == EntryKind.Cycle;
        set { if (value) SetKind(EntryKind.Cycle); }
    }

    private void SetKind(EntryKind kind)
    {
        if (_kind == kind) return;
        _kind = kind;
        OnPropertyChanged(nameof(KindDay));
        OnPropertyChanged(nameof(KindPeriod));
        OnPropertyChanged(nameof(KindCycle));
        OnPropertyChanged(nameof(IsSingleDay));
        OnPropertyChanged(nameof(IsPeriod));
        OnPropertyChanged(nameof(IsCycle));
        OnPropertyChanged(nameof(ShowSchedule));
        OnPropertyChanged(nameof(CyclePreviewText));
    }

    private DateTime _date;
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }

    private DateTime _startDate;
    public DateTime StartDate
    {
        get => _startDate;
        set { if (SetProperty(ref _startDate, value)) OnPropertyChanged(nameof(CyclePreviewText)); }
    }

    private DateTime _endDate;
    public DateTime EndDate
    {
        get => _endDate;
        set { if (SetProperty(ref _endDate, value)) OnPropertyChanged(nameof(CyclePreviewText)); }
    }

    private bool _includeWeekends;
    public bool IncludeWeekends
    {
        get => _includeWeekends;
        set { if (SetProperty(ref _includeWeekends, value)) OnPropertyChanged(nameof(CyclePreviewText)); }
    }

    // ---------- Cycle (rotation) ----------

    public ObservableCollection<CycleStepViewModel> CycleSteps { get; } = new();

    public string CyclePreviewText
    {
        get
        {
            if (CycleSteps.Count == 0) return "Ajoutez au moins un jour au cycle.";
            int days = BuildDates().Count;
            double repeats = days / (double)CycleSteps.Count;
            string we = _includeWeekends ? "" : ", week-ends exclus";
            return $"Cycle de {CycleSteps.Count} jour(s), répété {repeats:0.#} fois sur {days} jour(s){we}.";
        }
    }

    private void AddCycleStep()
    {
        var last = CycleSteps.LastOrDefault();
        CycleSteps.Add(last is null
            ? new CycleStepViewModel(CycleSteps.Count + 1)
            : new CycleStepViewModel(CycleSteps.Count + 1, last.Status, last.StartText, last.EndText,
                last.BreakMinutes, last.NormalHours, last.OvertimeHours));
        OnPropertyChanged(nameof(CyclePreviewText));
    }

    private void RemoveCycleStep(CycleStepViewModel? step)
    {
        if (step is null) return;
        CycleSteps.Remove(step);
        Renumber();
    }

    private void ApplyPreset(string preset)
    {
        CycleSteps.Clear();
        void Work() => CycleSteps.Add(new CycleStepViewModel(0, DayStatus.Travail, "08:00", "16:00", 0, 8, 0));
        void Rest() => CycleSteps.Add(new CycleStepViewModel(0, DayStatus.Repos, string.Empty, string.Empty, 0, 0, 0));

        switch (preset)
        {
            case "2/2": Work(); Work(); Rest(); Rest(); break;
            case "3/3": Work(); Work(); Work(); Rest(); Rest(); Rest(); break;
            case "4/4": for (int i = 0; i < 4; i++) Work(); for (int i = 0; i < 4; i++) Rest(); break;
            case "5/2": for (int i = 0; i < 5; i++) Work(); Rest(); Rest(); break;
            case "6/1": for (int i = 0; i < 6; i++) Work(); Rest(); break;
            default: Work(); Work(); Rest(); break;
        }
        Renumber();
    }

    private void Renumber()
    {
        for (int i = 0; i < CycleSteps.Count; i++) CycleSteps[i].Index = i + 1;
        OnPropertyChanged(nameof(CyclePreviewText));
    }

    // ---------- Mode d'application ----------

    private enum ApplyMode { Replace, Add, Skip }
    private ApplyMode _mode = ApplyMode.Replace;

    public bool ModeReplace
    {
        get => _mode == ApplyMode.Replace;
        set { if (value && _mode != ApplyMode.Replace) SetMode(ApplyMode.Replace); }
    }

    public bool ModeAdd
    {
        get => _mode == ApplyMode.Add;
        set
        {
            if (!value || _mode == ApplyMode.Add) return;
            SetMode(ApplyMode.Add);
            _normalHours = 0;
            _overtimeHours = 0;
            _note = string.Empty;
            _startText = string.Empty;
            _endText = string.Empty;
            OnPropertyChanged(nameof(NormalHours));
            OnPropertyChanged(nameof(OvertimeHours));
            OnPropertyChanged(nameof(Note));
            OnPropertyChanged(nameof(StartText));
            OnPropertyChanged(nameof(EndText));
            Status = DayStatus.HeuresSup;
        }
    }

    public bool ModeSkip
    {
        get => _mode == ApplyMode.Skip;
        set { if (value && _mode != ApplyMode.Skip) SetMode(ApplyMode.Skip); }
    }

    public bool IsAddMode => _mode == ApplyMode.Add;

    private void SetMode(ApplyMode mode)
    {
        _mode = mode;
        OnPropertyChanged(nameof(ModeReplace));
        OnPropertyChanged(nameof(ModeAdd));
        OnPropertyChanged(nameof(ModeSkip));
        OnPropertyChanged(nameof(IsAddMode));
    }

    // ---------- Journée / période simple ----------

    private DayStatus _status = DayStatus.Travail;
    public DayStatus Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HoursEnabled)); }
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
        if (span < 0) span += 24;
        span -= _breakMinutes / 60.0;
        span = Math.Max(0, Math.Round(span, 2));

        NormalHours = Math.Max(0, span - OvertimeHours);
        Error = null;
    }

    // ---------- Enregistrement ----------

    private void Save()
    {
        Error = null;

        var dates = BuildDates();
        if (dates.Count == 0)
        {
            Error = _kind == EntryKind.Day
                ? "Date invalide."
                : "La plage ne contient aucun jour (vérifiez les dates / les week-ends).";
            return;
        }

        double? rate = UseCustomRate && CustomRate > 0 ? CustomRate : null;
        string extraNote = _note?.Trim() ?? string.Empty;

        List<DayEntry> toSave;

        if (_kind == EntryKind.Cycle)
        {
            if (CycleSteps.Count == 0)
            {
                Error = "Ajoutez au moins un jour au cycle.";
                return;
            }
            // Un cycle possède sa plage : on remplace toujours (pas de cumul).
            _mode = ApplyMode.Replace;
            if (_editingCycleId > 0)
                _repo.DeleteRange(_originalCycleStart, _originalCycleEnd, notify: false);
            toSave = BuildCycleEntries(dates, rate, extraNote);
        }
        else
        {
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
            double overtime = HoursEnabled ? OvertimeHours : 0; // peut être négatif (heures retirées)

            toSave = new List<DayEntry>();
            foreach (var d in dates)
            {
                var entry = BuildEntry(d, _status, HoursEnabled ? start : null, HoursEnabled ? end : null,
                    HoursEnabled ? Math.Max(0, _breakMinutes) : 0, normal, overtime, rate, extraNote);
                if (entry is not null) toSave.Add(entry);
            }
        }

        if (toSave.Count == 0)
        {
            Error = _mode == ApplyMode.Skip
                ? "Tous les jours concernés sont déjà remplis."
                : "Aucune date à enregistrer.";
            return;
        }

        _repo.SaveMany(toSave);
        if (_kind == EntryKind.Cycle) PersistCyclePlan();
        LogSaved(dates);
        CloseRequested?.Invoke(true);
    }

    private void PersistCyclePlan()
    {
        var plan = new CyclePlan
        {
            Id = _editingCycleId,
            StartDate = DateOnly.FromDateTime(_startDate.Date),
            EndDate = DateOnly.FromDateTime(_endDate.Date),
            IncludeWeekends = _includeWeekends,
            HourlyRateOverride = UseCustomRate && CustomRate > 0 ? CustomRate : null,
            Note = _note?.Trim() ?? string.Empty,
            UpdatedAt = DateTime.Now,
            Steps = CycleSteps.Select(s => new CyclePlanStep
            {
                Status = (int)s.Status,
                Start = string.IsNullOrWhiteSpace(s.StartText) ? null : s.StartText,
                End = string.IsNullOrWhiteSpace(s.EndText) ? null : s.EndText,
                BreakMinutes = s.BreakMinutes,
                NormalHours = s.NormalHours,
                OvertimeHours = s.OvertimeHours
            }).ToList()
        };
        if (plan.EndDate < plan.StartDate) (plan.StartDate, plan.EndDate) = (plan.EndDate, plan.StartDate);
        _editingCycleId = _cycleRepo.Save(plan);
    }

    private List<DayEntry> BuildCycleEntries(List<DateOnly> dates, double? rate, string extraNote)
    {
        var list = new List<DayEntry>();
        for (int i = 0; i < dates.Count; i++)
        {
            var step = CycleSteps[i % CycleSteps.Count];
            bool working = step.Status.IsWorking();
            var entry = BuildEntry(
                dates[i], step.Status,
                working ? CycleStepViewModel.ParseTime(step.StartText) : null,
                working ? CycleStepViewModel.ParseTime(step.EndText) : null,
                working ? Math.Max(0, step.BreakMinutes) : 0,
                working ? Math.Max(0, step.NormalHours) : 0,
                working ? step.OvertimeHours : 0,
                rate, extraNote);
            if (entry is not null) list.Add(entry);
        }
        return list;
    }

    /// <summary>Construit (ou fusionne) l'entrée d'un jour selon le mode d'application. null = à ignorer.</summary>
    private DayEntry? BuildEntry(DateOnly d, DayStatus status, TimeOnly? start, TimeOnly? end,
        int breakMinutes, double normal, double overtime, double? rate, string extraNote)
    {
        var existing = _repo.Get(d);

        if (existing is not null && _mode == ApplyMode.Skip)
            return null;

        if (existing is not null && _mode == ApplyMode.Add)
        {
            var merged = existing.Clone();
            merged.NormalHours = Math.Max(0, existing.NormalHours + normal);
            merged.OvertimeHours = existing.OvertimeHours + overtime; // solde, peut devenir négatif
            if (!existing.Status.IsWorking()) merged.Status = status;
            merged.StartTime ??= start;
            merged.EndTime ??= end;
            if (merged.BreakMinutes == 0 && breakMinutes > 0) merged.BreakMinutes = breakMinutes;
            if (rate is not null) merged.HourlyRateOverride = rate;
            if (extraNote.Length > 0)
                merged.Note = string.IsNullOrWhiteSpace(existing.Note)
                    ? extraNote
                    : existing.Note + " · " + extraNote;
            merged.UpdatedAt = DateTime.Now;
            return merged;
        }

        return new DayEntry
        {
            Date = d,
            Status = status,
            StartTime = start,
            EndTime = end,
            BreakMinutes = breakMinutes,
            NormalHours = normal,
            OvertimeHours = overtime,
            HourlyRateOverride = rate,
            Note = extraNote,
            UpdatedAt = DateTime.Now
        };
    }

    private void LogSaved(List<DateOnly> dates)
    {
        string modeText = _mode switch
        {
            ApplyMode.Add => " · cumul des heures",
            ApplyMode.Skip => " · jours vides seulement",
            _ => string.Empty
        };

        switch (_kind)
        {
            case EntryKind.Cycle:
                var pattern = string.Join(" ", CycleSteps.Select(s => s.Status == DayStatus.Repos ? "R"
                    : s.Status == DayStatus.Conge ? "C" : "T"));
                string verb = IsEditingCycle ? "Cycle modifié" : "Cycle";
                _activityLog.Log(ActivityCategory.Periode,
                    $"{verb} [{pattern}] du {dates[0]:dd/MM/yyyy} au {dates[^1]:dd/MM/yyyy} — {dates.Count} jour(s)");
                break;

            case EntryKind.Period:
                string hours = _status.IsWorking()
                    ? $" — {Fmt.H(Math.Max(0, NormalHours))} + {Fmt.H(Math.Max(0, OvertimeHours))} sup."
                    : string.Empty;
                _activityLog.Log(ActivityCategory.Periode,
                    $"Période du {dates[0]:dd/MM/yyyy} au {dates[^1]:dd/MM/yyyy} — {dates.Count} jour(s) · {_status.Label()}{hours}{modeText}");
                break;

            default:
                string h = _status.IsWorking()
                    ? $" — {Fmt.H(Math.Max(0, NormalHours))} + {Fmt.H(Math.Max(0, OvertimeHours))} sup."
                    : string.Empty;
                _activityLog.Log(ActivityCategory.Saisie,
                    $"{dates[0]:dd/MM/yyyy} · {_status.Label()}{h}{modeText}");
                break;
        }
    }

    private List<DateOnly> BuildDates()
    {
        var result = new List<DateOnly>();

        if (_kind == EntryKind.Day)
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
            // Période comme cycle : week-ends exclus sauf si l'option est cochée.
            // Un cycle qui exclut les week-ends n'avance donc que les jours ouvrés.
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
