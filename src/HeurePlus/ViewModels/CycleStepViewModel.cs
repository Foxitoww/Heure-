using System;
using System.Collections.Generic;
using System.Linq;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Un jour du cycle (rotation) dans l'éditeur de saisie.</summary>
public sealed class CycleStepViewModel : ObservableObject
{
    public CycleStepViewModel(
        int index,
        DayStatus status = DayStatus.Travail,
        string start = "08:00",
        string end = "16:00",
        int breakMinutes = 0,
        double normalHours = 8,
        double overtimeHours = 0)
    {
        _index = index;
        _status = status;
        _startText = start;
        _endText = end;
        _breakMinutes = breakMinutes;
        _normalHours = normalHours;
        _overtimeHours = overtimeHours;

        ComputeCommand = new RelayCommand(_ => ComputeFromSchedule());
    }

    public IReadOnlyList<EntryEditorViewModel.StatusOption> StatusOptions { get; } =
        Enum.GetValues<DayStatus>()
            .Select(s => new EntryEditorViewModel.StatusOption(s, s.Label()))
            .ToArray();

    public RelayCommand ComputeCommand { get; }

    private int _index;
    public int Index
    {
        get => _index;
        set { if (SetProperty(ref _index, value)) OnPropertyChanged(nameof(Label)); }
    }

    public string Label => $"Jour {_index}";

    private DayStatus _status;
    public DayStatus Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HoursEnabled)); }
    }

    public bool HoursEnabled => _status.IsWorking();

    private string _startText;
    public string StartText { get => _startText; set { if (SetProperty(ref _startText, value)) ComputeFromSchedule(); } }

    private string _endText;
    public string EndText { get => _endText; set { if (SetProperty(ref _endText, value)) ComputeFromSchedule(); } }

    private int _breakMinutes;
    public int BreakMinutes { get => _breakMinutes; set { if (SetProperty(ref _breakMinutes, value)) ComputeFromSchedule(); } }

    private double _normalHours;
    public double NormalHours { get => _normalHours; set => SetProperty(ref _normalHours, value); }

    private double _overtimeHours;
    public double OvertimeHours { get => _overtimeHours; set => SetProperty(ref _overtimeHours, value); }

    public double TotalHours => HoursEnabled ? Math.Max(0, NormalHours) + Math.Max(0, OvertimeHours) : 0;

    private void ComputeFromSchedule()
    {
        var s = ParseTime(_startText);
        var e = ParseTime(_endText);
        if (s is null || e is null) return;

        double span = (e.Value - s.Value).TotalHours;
        if (span < 0) span += 24;
        span -= _breakMinutes / 60.0;
        span = Math.Max(0, Math.Round(span, 2));

        NormalHours = Math.Max(0, span - Math.Max(0, OvertimeHours));
    }

    public static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim().Replace('h', ':').Replace('H', ':');
        if (!text.Contains(':')) text += ":00";
        return TimeOnly.TryParse(text, out var t) ? t : null;
    }
}
