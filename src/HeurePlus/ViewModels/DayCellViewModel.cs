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
            OnPropertyChanged(nameof(HoursLabel));
        }
    }

    public bool HasEntry => _entry is not null;

    /// <summary>Clé utilisée par le convertisseur de couleur ("Travail", "Conge"… ou "None").</summary>
    public string StatusKey => _entry?.Status.ToString() ?? "None";

    public string HoursLabel =>
        _entry is { } e && e.TotalHours > 0 ? Fmt.H(e.TotalHours) : string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
