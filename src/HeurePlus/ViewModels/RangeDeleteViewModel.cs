using System;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Petite fenêtre : supprimer toutes les saisies d'une plage de dates.</summary>
public sealed class RangeDeleteViewModel : ObservableObject
{
    private readonly DayEntryRepository _repo;
    private readonly ActivityLogRepository _activityLog;

    public event Action<bool>? CloseRequested;

    public RangeDeleteViewModel(DayEntryRepository repo, ActivityLogRepository activityLog, DateOnly anchor)
    {
        _repo = repo;
        _activityLog = activityLog;

        _startDate = anchor.ToDateTime(default);
        _endDate = _startDate;

        ConfirmCommand = new RelayCommand(_ => Confirm());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        RefreshCount();
    }

    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }

    private DateTime _startDate;
    public DateTime StartDate { get => _startDate; set { if (SetProperty(ref _startDate, value)) RefreshCount(); } }

    private DateTime _endDate;
    public DateTime EndDate { get => _endDate; set { if (SetProperty(ref _endDate, value)) RefreshCount(); } }

    private string _countText = "";
    public string CountText { get => _countText; private set => SetProperty(ref _countText, value); }

    private (DateOnly from, DateOnly to) Range()
    {
        var from = DateOnly.FromDateTime(_startDate.Date);
        var to = DateOnly.FromDateTime(_endDate.Date);
        if (to < from) (from, to) = (to, from);
        return (from, to);
    }

    private void RefreshCount()
    {
        var (from, to) = Range();
        int n = _repo.GetRange(from, to).Count;
        CountText = n == 0
            ? "Aucune saisie dans cette plage."
            : $"{n} saisie(s) seront définitivement supprimées.";
    }

    private void Confirm()
    {
        var (from, to) = Range();
        int n = _repo.GetRange(from, to).Count;
        if (n == 0) { CloseRequested?.Invoke(false); return; }

        _repo.DeleteRange(from, to);
        _activityLog.Log(ActivityCategory.Suppression,
            $"{n} saisie(s) supprimées du {from:dd/MM/yyyy} au {to:dd/MM/yyyy}");
        CloseRequested?.Invoke(true);
    }
}
