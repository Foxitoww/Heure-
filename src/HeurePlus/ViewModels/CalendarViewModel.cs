using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

public sealed class CalendarViewModel : ObservableObject
{
    private const int TotalCells = 42; // 6 semaines x 7 jours

    private readonly DayEntryRepository _repo;
    private readonly SettingsRepository _settingsRepo;
    private readonly ActivityLogRepository _activityLog;
    private readonly CycleRepository _cycleRepo;
    private readonly AppEvents _events;
    private readonly Func<EntryEditorViewModel, bool?> _showEditor;
    private readonly Func<RangeDeleteViewModel, bool?> _showRangeDelete;
    private readonly Func<CyclesViewModel, bool?> _showCyclesManager;

    private DayOfWeek _firstDayOfWeek;

    public CalendarViewModel(
        DayEntryRepository repo,
        SettingsRepository settingsRepo,
        ActivityLogRepository activityLog,
        CycleRepository cycleRepo,
        AppEvents events,
        Func<EntryEditorViewModel, bool?> showEditor,
        Func<RangeDeleteViewModel, bool?> showRangeDelete,
        Func<CyclesViewModel, bool?> showCyclesManager)
    {
        _repo = repo;
        _settingsRepo = settingsRepo;
        _activityLog = activityLog;
        _cycleRepo = cycleRepo;
        _events = events;
        _showEditor = showEditor;
        _showRangeDelete = showRangeDelete;
        _showCyclesManager = showCyclesManager;

        _firstDayOfWeek = settingsRepo.LoadApp().FirstDayOfWeek;
        _visibleMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        PrevMonthCommand = new RelayCommand(_ => MoveMonth(-1));
        NextMonthCommand = new RelayCommand(_ => MoveMonth(+1));
        TodayCommand = new RelayCommand(_ => GoToToday());
        SelectDayCommand = new RelayCommand(p => { if (p is DayCellViewModel c) SelectedDay = c; });
        AddEntryCommand = new RelayCommand(_ => OpenEditor(EntryKind.Day));
        AddPeriodCommand = new RelayCommand(_ => OpenEditor(EntryKind.Period));
        AddCycleCommand = new RelayCommand(_ => OpenCyclesManager());
        EditSelectedCommand = new RelayCommand(_ => OpenEditor(EntryKind.Day), _ => SelectedDay is not null);
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedDay?.Entry is not null);
        DeleteRangeCommand = new RelayCommand(_ => DeleteRange());
        EditCycleCommand = new RelayCommand(_ => EditCycle(), _ => _selectedDayCycle is not null);

        _events.EntriesChanged += ReloadKeepingSelection;
        _events.SettingsChanged += OnSettingsChanged;

        BuildWeekdayHeaders();
        LoadMonth();
        GoToToday();
    }

    // ---------- État ----------

    private DateOnly _visibleMonth;

    public string MonthLabel => Fmt.Fr.TextInfo.ToTitleCase(
        _visibleMonth.ToDateTime(default).ToString("MMMM yyyy", Fmt.Fr));

    public ObservableCollection<string> WeekdayHeaders { get; } = new();

    public ObservableCollection<DayCellViewModel> Days { get; } = new();

    public DayDetailsViewModel Details { get; } = new();

    private HeurePlus.Models.CyclePlan? _selectedDayCycle;

    private DayCellViewModel? _selectedDay;
    public DayCellViewModel? SelectedDay
    {
        get => _selectedDay;
        set
        {
            if (_selectedDay is not null) _selectedDay.IsSelected = false;
            _selectedDay = value;
            if (_selectedDay is not null) _selectedDay.IsSelected = true;

            _selectedDayCycle = _selectedDay is null ? null : _cycleRepo.GetForDate(_selectedDay.Date);

            OnPropertyChanged();
            Details.SetCell(_selectedDay, _selectedDayCycle);
            EditSelectedCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            EditCycleCommand.RaiseCanExecuteChanged();
        }
    }

    // ---------- Commandes ----------

    public RelayCommand PrevMonthCommand { get; }
    public RelayCommand NextMonthCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand SelectDayCommand { get; }
    public RelayCommand AddEntryCommand { get; }
    public RelayCommand AddPeriodCommand { get; }
    public RelayCommand AddCycleCommand { get; }
    public RelayCommand EditSelectedCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand DeleteRangeCommand { get; }
    public RelayCommand EditCycleCommand { get; }

    // ---------- Logique ----------

    private void MoveMonth(int delta)
    {
        _visibleMonth = _visibleMonth.AddMonths(delta);
        OnPropertyChanged(nameof(MonthLabel));
        LoadMonth();
    }

    private void GoToToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today.Year != _visibleMonth.Year || today.Month != _visibleMonth.Month)
        {
            _visibleMonth = new DateOnly(today.Year, today.Month, 1);
            OnPropertyChanged(nameof(MonthLabel));
            LoadMonth();
        }
        SelectedDay = Days.FirstOrDefault(d => d.Date == today) ?? Days.FirstOrDefault(d => d.IsCurrentMonth);
    }

    private void OnSettingsChanged()
    {
        var first = _settingsRepo.LoadApp().FirstDayOfWeek;
        if (first != _firstDayOfWeek)
        {
            _firstDayOfWeek = first;
            BuildWeekdayHeaders();
        }
        LoadMonth();
    }

    private void BuildWeekdayHeaders()
    {
        WeekdayHeaders.Clear();
        var names = Fmt.Fr.DateTimeFormat.AbbreviatedDayNames; // dimanche = index 0
        for (int i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)_firstDayOfWeek + i) % 7);
            WeekdayHeaders.Add(Fmt.Fr.TextInfo.ToTitleCase(names[(int)day]).TrimEnd('.'));
        }
    }

    private void LoadMonth()
    {
        var firstOfMonth = _visibleMonth;
        int offset = ((int)firstOfMonth.DayOfWeek - (int)_firstDayOfWeek + 7) % 7;
        var firstVisible = firstOfMonth.AddDays(-offset);
        var lastVisible = firstVisible.AddDays(TotalCells - 1);

        var entries = _repo.GetRange(firstVisible, lastVisible)
            .ToDictionary(e => e.Date);

        DateOnly? keepDate = _selectedDay?.Date;

        Days.Clear();
        for (int i = 0; i < TotalCells; i++)
        {
            var date = firstVisible.AddDays(i);
            var cell = new DayCellViewModel(date, date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year)
            {
                HolidayName = Services.FrenchHolidays.NameOf(date)
            };
            if (entries.TryGetValue(date, out var entry)) cell.Entry = entry;
            Days.Add(cell);
        }

        _selectedDay = null;
        if (keepDate is { } d)
            SelectedDay = Days.FirstOrDefault(c => c.Date == d);
        else
            Details.SetCell(null);
    }

    private void ReloadKeepingSelection() => LoadMonth();

    private void OpenEditor(EntryKind kind)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        bool todayVisible = today.Month == _visibleMonth.Month && today.Year == _visibleMonth.Year;
        DateOnly anchor = SelectedDay?.Date ?? (todayVisible ? today : _visibleMonth);

        var existing = kind == EntryKind.Day && SelectedDay is not null ? _repo.Get(SelectedDay.Date) : null;
        var salary = _settingsRepo.LoadSalary();

        var editor = new EntryEditorViewModel(_repo, _activityLog, _cycleRepo, salary, anchor, existing, kind);
        RunEditor(editor, anchor);
    }

    private void EditCycle()
    {
        if (_selectedDayCycle is null) return;
        var anchor = _selectedDayCycle.StartDate;
        var salary = _settingsRepo.LoadSalary();
        var editor = new EntryEditorViewModel(_repo, _activityLog, _cycleRepo, salary, anchor, null,
            EntryKind.Cycle, _selectedDayCycle);
        RunEditor(editor, SelectedDay?.Date ?? anchor);
    }

    private void OpenCyclesManager()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        bool todayVisible = today.Month == _visibleMonth.Month && today.Year == _visibleMonth.Year;
        var month = todayVisible ? today : _visibleMonth;

        var manager = new CyclesViewModel(_cycleRepo, _repo, _activityLog, _settingsRepo, _showEditor, month);
        _showCyclesManager(manager);
        LoadMonth();
    }

    private void RunEditor(EntryEditorViewModel editor, DateOnly reselect)
    {
        if (_showEditor(editor) == true)
        {
            // EntriesChanged a déjà rechargé le mois ; on resélectionne la date d'ancrage.
            var target = Days.FirstOrDefault(c => c.Date == reselect);
            if (target is not null) SelectedDay = target;
        }
    }

    private void DeleteRange()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        bool todayVisible = today.Month == _visibleMonth.Month && today.Year == _visibleMonth.Year;
        DateOnly anchor = SelectedDay?.Date ?? (todayVisible ? today : _visibleMonth);
        _showRangeDelete(new RangeDeleteViewModel(_repo, _activityLog, anchor));
    }

    private void DeleteSelected()
    {
        if (SelectedDay?.Entry is null) return;

        var answer = MessageBox.Show(
            $"Supprimer la saisie du {SelectedDay.Date:dd/MM/yyyy} ?",
            "Heure+",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        var date = SelectedDay.Date;
        _repo.Delete(date);
        _activityLog.Log(Models.ActivityCategory.Suppression, $"Saisie supprimée — {date:dd/MM/yyyy}");
    }
}
