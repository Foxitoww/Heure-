using System;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Une case du calendrier mensuel.</summary>
public sealed class DayCellViewModel : ObservableObject
{
    public DayCellViewModel(DateOnly date, bool isCurrentMonth)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = date == DateOnly.FromDateTime(DateTime.Today);
    }

    public DateOnly Date { get; }

    public bool IsCurrentMonth { get; }

    public bool IsToday { get; }

    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public int DayNumber => Date.Day;

    /// <summary>Nom du jour férié français (null sinon), renseigné au chargement du mois.</summary>
    public string? HolidayName { get; set; }

    public bool IsHoliday => HolidayName is not null;

    private DayEntry? _entry;
    public DayEntry? Entry
    {
        get => _entry;
        set
        {
            _entry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEntry));
            OnPropertyChanged(nameof(StatusKey));
            OnPropertyChanged(nameof(BarColorKey));
            OnPropertyChanged(nameof(HoursLabel));
        }
    }

    public bool HasEntry => _entry is not null;

    /// <summary>Clé du statut brut ("Travail", "Conge"… ou "None"), pour la pastille du panneau de détails.</summary>
    public string StatusKey => _entry?.Status.ToString() ?? "None";

    /// <summary>
    /// Clé de couleur du trait sous le jour : rouge si heures retirées, vert si heures sup.,
    /// sinon la couleur du statut (repos = jaune, congé = violet, travail = bleu).
    /// </summary>
    public string BarColorKey
    {
        get
        {
            if (_entry is null) return "None";
            if (_entry.OvertimeHours < 0) return "Retrait";
            if (_entry.OvertimeHours > 0) return "HeuresSup";
            return _entry.Status.ToString();
        }
    }

    public string HoursLabel =>
        _entry is { } e && e.TotalHours > 0 ? Fmt.H(e.TotalHours) : string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
