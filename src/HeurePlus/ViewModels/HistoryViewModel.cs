using System;
using System.Collections.ObjectModel;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Ligne affichée dans l'onglet Historique.</summary>
public sealed class HistoryItem
{
    public HistoryItem(ActivityEntry entry)
    {
        Category = entry.CategoryLabel;
        Description = entry.Description;
        When = RelativeTime(entry.Timestamp);
        Exact = Fmt.Fr.TextInfo.ToTitleCase(entry.Timestamp.ToString("dddd d MMMM yyyy", Fmt.Fr))
                + " · " + entry.Timestamp.ToString("HH:mm", Fmt.Fr);
    }

    public string Category { get; }
    public string Description { get; }
    public string When { get; }
    public string Exact { get; }

    private static string RelativeTime(DateTime timestamp)
    {
        var now = DateTime.Now;
        var span = now - timestamp;

        if (span < TimeSpan.Zero) return timestamp.ToString("HH:mm", Fmt.Fr);
        if (span < TimeSpan.FromMinutes(1)) return "à l'instant";
        if (span < TimeSpan.FromHours(1)) return $"il y a {(int)span.TotalMinutes} min";
        if (timestamp.Date == now.Date) return $"aujourd'hui {timestamp:HH\\:mm}";
        if (timestamp.Date == now.Date.AddDays(-1)) return $"hier {timestamp:HH\\:mm}";
        if (span < TimeSpan.FromDays(7))
            return Fmt.Fr.TextInfo.ToTitleCase(timestamp.ToString("dddd", Fmt.Fr)) + timestamp.ToString(" HH:mm", Fmt.Fr);
        return timestamp.ToString("dd/MM/yyyy HH:mm", Fmt.Fr);
    }
}

public sealed class HistoryViewModel : ObservableObject
{
    private readonly ActivityLogRepository _log;
    private readonly AppEvents _events;

    public HistoryViewModel(ActivityLogRepository log, AppEvents events)
    {
        _log = log;
        _events = events;

        ClearCommand = new RelayCommand(_ => Clear(), _ => Items.Count > 0);
        RefreshCommand = new RelayCommand(_ => Load());

        _events.ActivityLogged += OnActivityLogged;
        Load();
    }

    public ObservableCollection<HistoryItem> Items { get; } = new();

    public bool IsEmpty => Items.Count == 0;

    public string CountText => Items.Count switch
    {
        0 => "Aucune action enregistrée pour le moment.",
        1 => "1 action enregistrée",
        var n => $"{n} actions enregistrées"
    };

    public RelayCommand ClearCommand { get; }
    public RelayCommand RefreshCommand { get; }

    private void OnActivityLogged() => Application.Current?.Dispatcher.Invoke(Load);

    private void Load()
    {
        Items.Clear();
        foreach (var entry in _log.GetRecent())
            Items.Add(new HistoryItem(entry));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
        ClearCommand.RaiseCanExecuteChanged();
    }

    private void Clear()
    {
        if (MessageBox.Show(
                "Effacer tout l'historique des actions ? Les saisies du calendrier ne sont pas touchées.",
                "Heure+", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _log.Clear(); // déclenche ActivityLogged -> Load()
    }
}
