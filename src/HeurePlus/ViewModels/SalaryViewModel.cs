using System;
using System.Collections.Generic;
using System.Linq;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;

namespace HeurePlus.ViewModels;

public sealed class SalaryViewModel : ObservableObject
{
    private readonly DayEntryRepository _entries;
    private readonly SettingsRepository _settingsRepo;
    private readonly AppEvents _events;

    public SalaryViewModel(DayEntryRepository entries, SettingsRepository settingsRepo, AppEvents events)
    {
        _entries = entries;
        _settingsRepo = settingsRepo;
        _events = events;

        LoadFromSettings(_settingsRepo.LoadSalary());

        SaveCommand = new RelayCommand(_ => Save());
        ResetCommand = new RelayCommand(_ => LoadFromSettings(_settingsRepo.LoadSalary()));

        _events.EntriesChanged += Recompute;
        Recompute();
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }

    public IReadOnlyList<string> ScopeOptions { get; } = new[]
    {
        "Ce mois-ci", "Mois précédent", "Toute la période"
    };

    private string _selectedScope = "Ce mois-ci";
    public string SelectedScope
    {
        get => _selectedScope;
        set { if (SetProperty(ref _selectedScope, value)) Recompute(); }
    }

    // ---------- Champs éditables ----------

    private double _hourlyRate;
    public double HourlyRate { get => _hourlyRate; set { if (SetProperty(ref _hourlyRate, value)) Recompute(); } }

    private double _overtimeMultiplier;
    public double OvertimeMultiplier { get => _overtimeMultiplier; set { if (SetProperty(ref _overtimeMultiplier, value)) Recompute(); } }

    private bool _applyIfm;
    public bool ApplyIfm { get => _applyIfm; set { if (SetProperty(ref _applyIfm, value)) Recompute(); } }

    private double _ifmPercent;
    public double IfmPercent { get => _ifmPercent; set { if (SetProperty(ref _ifmPercent, value)) Recompute(); } }

    private bool _applyIcp;
    public bool ApplyIcp { get => _applyIcp; set { if (SetProperty(ref _applyIcp, value)) Recompute(); } }

    private double _icpPercent;
    public double IcpPercent { get => _icpPercent; set { if (SetProperty(ref _icpPercent, value)) Recompute(); } }

    private double _weeklyHours;
    public double WeeklyHours { get => _weeklyHours; set => SetProperty(ref _weeklyHours, value); }

    private string _currency = "€";
    public string Currency
    {
        get => _currency;
        set { if (SetProperty(ref _currency, value)) Recompute(); }
    }

    // ---------- Résultat (aperçu, non enregistré) ----------

    public string ScopeLabel { get; private set; } = "";
    public string NormalHoursText { get; private set; } = "";
    public string NormalPayText { get; private set; } = "";
    public string OvertimeHoursText { get; private set; } = "";
    public string OvertimePayText { get; private set; } = "";
    public string GrossText { get; private set; } = "";
    public string IfmText { get; private set; } = "";
    public string IcpText { get; private set; } = "";
    public string TotalText { get; private set; } = "";
    public bool ShowIfm { get; private set; }
    public bool ShowIcp { get; private set; }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    // ---------- Implémentation ----------

    private void LoadFromSettings(SalarySettings s)
    {
        _hourlyRate = s.HourlyRate;
        _overtimeMultiplier = s.OvertimeMultiplier;
        _applyIfm = s.ApplyEndOfMissionBonus;
        _ifmPercent = Math.Round(s.EndOfMissionRate * 100, 2);
        _applyIcp = s.ApplyPaidLeaveBonus;
        _icpPercent = Math.Round(s.PaidLeaveRate * 100, 2);
        _weeklyHours = s.WeeklyHours;
        _currency = s.Currency;
        RaiseAll();
        Recompute();
    }

    private SalarySettings CurrentSettings() => new()
    {
        HourlyRate = HourlyRate,
        OvertimeMultiplier = OvertimeMultiplier,
        ApplyEndOfMissionBonus = ApplyIfm,
        EndOfMissionRate = IfmPercent / 100.0,
        ApplyPaidLeaveBonus = ApplyIcp,
        PaidLeaveRate = IcpPercent / 100.0,
        WeeklyHours = WeeklyHours,
        Currency = string.IsNullOrWhiteSpace(Currency) ? "€" : Currency
    };

    private void Save()
    {
        _settingsRepo.SaveSalary(CurrentSettings());
        StatusMessage = "Réglages de salaire enregistrés.";
    }

    private void Recompute()
    {
        var settings = CurrentSettings();
        var (from, to, label) = ResolveScope();
        ScopeLabel = label;

        var data = from is null || to is null
            ? _entries.GetAll()
            : _entries.GetRange(from.Value, to.Value);

        var result = SalaryCalculator.Estimate(data, settings);

        NormalHoursText = Fmt.H(result.NormalHours);
        NormalPayText = Fmt.Money(result.NormalPay, settings.Currency);
        OvertimeHoursText = Fmt.H(result.OvertimeHours);
        OvertimePayText = Fmt.Money(result.OvertimePay, settings.Currency);
        GrossText = Fmt.Money(result.Gross, settings.Currency);

        ShowIfm = settings.ApplyEndOfMissionBonus;
        ShowIcp = settings.ApplyPaidLeaveBonus;
        IfmText = Fmt.Money(result.EndOfMissionBonus, settings.Currency);
        IcpText = Fmt.Money(result.PaidLeaveBonus, settings.Currency);
        TotalText = Fmt.Money(result.Total, settings.Currency);

        OnPropertyChanged(nameof(ScopeLabel));
        OnPropertyChanged(nameof(NormalHoursText));
        OnPropertyChanged(nameof(NormalPayText));
        OnPropertyChanged(nameof(OvertimeHoursText));
        OnPropertyChanged(nameof(OvertimePayText));
        OnPropertyChanged(nameof(GrossText));
        OnPropertyChanged(nameof(ShowIfm));
        OnPropertyChanged(nameof(ShowIcp));
        OnPropertyChanged(nameof(IfmText));
        OnPropertyChanged(nameof(IcpText));
        OnPropertyChanged(nameof(TotalText));
    }

    private (DateOnly? From, DateOnly? To, string Label) ResolveScope()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        switch (SelectedScope)
        {
            case "Mois précédent":
                var prev = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                return (prev, prev.AddMonths(1).AddDays(-1),
                    Fmt.Fr.TextInfo.ToTitleCase(prev.ToDateTime(default).ToString("MMMM yyyy", Fmt.Fr)));
            case "Toute la période":
                return (null, null, "Toutes les saisies");
            default:
                var first = new DateOnly(today.Year, today.Month, 1);
                return (first, first.AddMonths(1).AddDays(-1),
                    Fmt.Fr.TextInfo.ToTitleCase(first.ToDateTime(default).ToString("MMMM yyyy", Fmt.Fr)));
        }
    }
}
