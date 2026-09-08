using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Ligne du gestionnaire de cycles.</summary>
public sealed class CycleRowViewModel
{
    public CycleRowViewModel(CyclePlan plan, Action<CyclePlan> edit, Action<CyclePlan> delete)
    {
        Plan = plan;
        EditCommand = new RelayCommand(_ => edit(plan));
        DeleteCommand = new RelayCommand(_ => delete(plan));
    }

    public CyclePlan Plan { get; }

    public string RangeLabel => $"Du {Plan.StartDate:dd/MM/yyyy} au {Plan.EndDate:dd/MM/yyyy}";

    public string PatternText
    {
        get
        {
            var codes = Plan.Steps.Select(s => (DayStatus)s.Status switch
            {
                DayStatus.Repos => "Repos",
                DayStatus.Conge => "Congé",
                DayStatus.HeuresSup => "H. sup.",
                _ => "Travail"
            });
            return $"{Plan.Steps.Count} jour(s) : " + string.Join(" · ", codes);
        }
    }

    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
}

public sealed class CyclesViewModel : ObservableObject
{
    private readonly CycleRepository _cycleRepo;
    private readonly DayEntryRepository _repo;
    private readonly ActivityLogRepository _activityLog;
    private readonly SettingsRepository _settingsRepo;
    private readonly Func<EntryEditorViewModel, bool?> _showEditor;
    private readonly DateOnly _defaultMonth;

    public event Action<bool>? CloseRequested;

    public CyclesViewModel(
        CycleRepository cycleRepo,
        DayEntryRepository repo,
        ActivityLogRepository activityLog,
        SettingsRepository settingsRepo,
        Func<EntryEditorViewModel, bool?> showEditor,
        DateOnly defaultMonth)
    {
        _cycleRepo = cycleRepo;
        _repo = repo;
        _activityLog = activityLog;
        _settingsRepo = settingsRepo;
        _showEditor = showEditor;
        _defaultMonth = new DateOnly(defaultMonth.Year, defaultMonth.Month, 1);

        NewCycleCommand = new RelayCommand(_ => NewCycle());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(_anyChange));

        Load();
    }

    public ObservableCollection<CycleRowViewModel> Cycles { get; } = new();

    public bool IsEmpty => Cycles.Count == 0;

    public RelayCommand NewCycleCommand { get; }
    public RelayCommand CloseCommand { get; }

    private bool _anyChange;

    private void Load()
    {
        Cycles.Clear();
        foreach (var plan in _cycleRepo.GetAll())
            Cycles.Add(new CycleRowViewModel(plan, EditCycle, DeleteCycle));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private EntryEditorViewModel MakeEditor(CyclePlan? plan, DateOnly anchor) =>
        new(_repo, _activityLog, _cycleRepo, _settingsRepo.LoadSalary(), anchor, null, EntryKind.Cycle, plan);

    private void NewCycle()
    {
        if (_showEditor(MakeEditor(null, _defaultMonth)) == true)
        {
            _anyChange = true;
            Load();
        }
    }

    private void EditCycle(CyclePlan plan)
    {
        if (_showEditor(MakeEditor(plan, plan.StartDate)) == true)
        {
            _anyChange = true;
            Load();
        }
    }

    private void DeleteCycle(CyclePlan plan)
    {
        var answer = MessageBox.Show(
            $"Supprimer ce cycle ({plan.RangeLabel}) ?\n" +
            "Les saisies générées sur cette plage seront également supprimées.",
            "Heure+", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        _repo.DeleteRange(plan.StartDate, plan.EndDate);
        _cycleRepo.Delete(plan.Id);
        _activityLog.Log(ActivityCategory.Suppression, $"Cycle supprimé — {plan.RangeLabel}");
        _anyChange = true;
        Load();
    }
}
