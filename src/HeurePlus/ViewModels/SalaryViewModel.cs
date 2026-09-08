using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly ActivityLogRepository _activityLog;
    private readonly AppEvents _events;

    public SalaryViewModel(DayEntryRepository entries, SettingsRepository settingsRepo,
        ActivityLogRepository activityLog, AppEvents events)
    {
        _entries = entries;
        _settingsRepo = settingsRepo;
        _activityLog = activityLog;
        _events = events;

        SaveCommand = new RelayCommand(_ => Save());
        ResetCommand = new RelayCommand(_ => LoadFromSettings());
        ApplySmicCommand = new RelayCommand(_ => ApplySmic());
        ShowSectionParamsCommand = new RelayCommand(_ => Section = 0);
        ShowSectionPrimesCommand = new RelayCommand(_ => Section = 1);
        AddPrimeCommand = new RelayCommand(p => { if (p is Prime prime) AddPrime(prime); });
        RemovePrimeCommand = new RelayCommand(p => { if (p is AppliedPrimeViewModel vm) RemovePrime(vm); });

        LoadFromSettings();
        RefreshPrimeResults();

        _events.EntriesChanged += Recompute;
        Recompute();
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ApplySmicCommand { get; }
    public RelayCommand ShowSectionParamsCommand { get; }
    public RelayCommand ShowSectionPrimesCommand { get; }
    public RelayCommand AddPrimeCommand { get; }
    public RelayCommand RemovePrimeCommand { get; }

    // ---------- Sous-onglet ----------

    private int _section;
    public int Section
    {
        get => _section;
        set
        {
            if (!SetProperty(ref _section, value)) return;
            OnPropertyChanged(nameof(SectionIsParams));
            OnPropertyChanged(nameof(SectionIsPrimes));
        }
    }
    public bool SectionIsParams => _section == 0;
    public bool SectionIsPrimes => _section == 1;

    // ---------- Portée de l'estimation ----------

    public IReadOnlyList<string> ScopeOptions { get; } = new[] { "Ce mois-ci", "Mois précédent", "Toute la période" };

    private string _selectedScope = "Ce mois-ci";
    public string SelectedScope
    {
        get => _selectedScope;
        set { if (SetProperty(ref _selectedScope, value)) Recompute(); }
    }

    // ---------- Paramètres ----------

    private double _hourlyRate;
    public double HourlyRate { get => _hourlyRate; set { if (SetProperty(ref _hourlyRate, value)) { Recompute(); OnPropertyChanged(nameof(SmicComparisonText)); } } }

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
    public string Currency { get => _currency; set { if (SetProperty(ref _currency, value)) Recompute(); } }

    // ---------- SMIC ----------

    private double _smicHourly;
    public double SmicHourly
    {
        get => _smicHourly;
        set { if (SetProperty(ref _smicHourly, value)) OnPropertyChanged(nameof(SmicComparisonText)); }
    }

    private double _smicCoef;
    public double SmicCoef
    {
        get => _smicCoef;
        set { if (SetProperty(ref _smicCoef, value)) OnPropertyChanged(nameof(SmicComparisonText)); }
    }

    private bool _payHolidays;
    public bool PayHolidays { get => _payHolidays; set { if (SetProperty(ref _payHolidays, value)) Recompute(); } }

    private double _holidayHours;
    public double HolidayHours { get => _holidayHours; set { if (SetProperty(ref _holidayHours, value)) Recompute(); } }

    public string SmicComparisonText
    {
        get
        {
            if (_smicHourly <= 0) return "";
            double target = Math.Round(_smicHourly * _smicCoef, 2);
            double diff = _hourlyRate - _smicHourly;
            double pct = _smicHourly > 0 ? diff / _smicHourly * 100 : 0;
            string rel = Math.Abs(pct) < 0.05 ? "au SMIC"
                : pct > 0 ? $"SMIC +{pct:0.#} %"
                : $"SMIC {pct:0.#} %";
            return $"SMIC de référence : {Fmt.Money(_smicHourly, Currency)}/h · votre taux actuel : {rel} · coeff. → {Fmt.Money(target, Currency)}/h";
        }
    }

    private void ApplySmic()
    {
        HourlyRate = Math.Round(_smicHourly * (_smicCoef <= 0 ? 1.0 : _smicCoef), 2);
        StatusMessage = $"Taux horaire aligné sur le SMIC ({Fmt.Money(HourlyRate, Currency)}/h).";
    }

    // ---------- Primes ----------

    public ObservableCollection<AppliedPrimeViewModel> AppliedPrimes { get; } = new();
    public ObservableCollection<Prime> PrimeResults { get; } = new();

    private string _primeSearch = "";
    public string PrimeSearch
    {
        get => _primeSearch;
        set { if (SetProperty(ref _primeSearch, value)) RefreshPrimeResults(); }
    }

    public bool HasPrimes => AppliedPrimes.Count > 0;

    private void RefreshPrimeResults()
    {
        var taken = AppliedPrimes.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var q = _primeSearch?.Trim() ?? "";

        IEnumerable<Prime> matches = PrimeCatalog.All.Where(p => !taken.Contains(p.Name));
        if (q.Length > 0)
            matches = matches.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(q, StringComparison.OrdinalIgnoreCase));

        // Sans recherche on borne l'affichage ; une requête montre tout ce qui correspond.
        PrimeResults.Clear();
        foreach (var p in matches.Take(q.Length > 0 ? 200 : 60)) PrimeResults.Add(p);
    }

    private void AddPrime(Prime prime)
    {
        var model = new AppliedPrime
        {
            Name = prime.Name,
            Category = prime.Category,
            Unit = prime.Unit,
            Amount = prime.DefaultAmount,
            Enabled = true
        };
        AppliedPrimes.Add(new AppliedPrimeViewModel(model, OnPrimesChanged));
        _activityLog.Log(ActivityCategory.Salaire, $"Prime ajoutée — {prime.Name}");
        OnPrimesChanged();
    }

    private void RemovePrime(AppliedPrimeViewModel vm)
    {
        AppliedPrimes.Remove(vm);
        OnPrimesChanged();
    }

    private void OnPrimesChanged()
    {
        _settingsRepo.SavePrimes(AppliedPrimes.Select(v => v.Model));
        OnPropertyChanged(nameof(HasPrimes));
        RefreshPrimeResults();
        Recompute();
    }

    // ---------- Résultat (aperçu) ----------

    public string ScopeLabel { get; private set; } = "";
    public string NormalHoursText { get; private set; } = "";
    public string NormalPayText { get; private set; } = "";
    public string OvertimeHoursText { get; private set; } = "";
    public string OvertimePayText { get; private set; } = "";
    public string PrimesText { get; private set; } = "";
    public string GrossText { get; private set; } = "";
    public string IfmText { get; private set; } = "";
    public string IcpText { get; private set; } = "";
    public string TotalText { get; private set; } = "";
    public bool ShowPrimesLine { get; private set; }
    public bool ShowIfm { get; private set; }
    public bool ShowIcp { get; private set; }

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    // ---------- Implémentation ----------

    private void LoadFromSettings()
    {
        var s = _settingsRepo.LoadSalary();
        _hourlyRate = s.HourlyRate;
        _overtimeMultiplier = s.OvertimeMultiplier;
        _applyIfm = s.ApplyEndOfMissionBonus;
        _ifmPercent = Math.Round(s.EndOfMissionRate * 100, 2);
        _applyIcp = s.ApplyPaidLeaveBonus;
        _icpPercent = Math.Round(s.PaidLeaveRate * 100, 2);
        _weeklyHours = s.WeeklyHours;
        _smicHourly = s.SmicHourly;
        _smicCoef = s.SmicCoefficient;
        _payHolidays = s.PayPublicHolidays;
        _holidayHours = s.PublicHolidayHours;
        _currency = s.Currency;

        AppliedPrimes.Clear();
        foreach (var p in _settingsRepo.LoadPrimes())
            AppliedPrimes.Add(new AppliedPrimeViewModel(p, OnPrimesChanged));

        RaiseAll();
        RefreshPrimeResults();
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
        SmicHourly = SmicHourly,
        SmicCoefficient = SmicCoef,
        PayPublicHolidays = PayHolidays,
        PublicHolidayHours = HolidayHours,
        Currency = string.IsNullOrWhiteSpace(Currency) ? "€" : Currency
    };

    private void Save()
    {
        var settings = CurrentSettings();
        _settingsRepo.SaveSalary(settings);
        _settingsRepo.SavePrimes(AppliedPrimes.Select(v => v.Model));
        StatusMessage = "Réglages de salaire enregistrés.";
        _activityLog.Log(ActivityCategory.Salaire,
            $"Réglages de salaire — taux {Fmt.Money(settings.HourlyRate, settings.Currency)}/h · "
            + $"heures sup. x{settings.OvertimeMultiplier.ToString("0.##", Fmt.Fr)}"
            + (AppliedPrimes.Count > 0 ? $" · {AppliedPrimes.Count} prime(s)" : ""));
    }

    private void Recompute()
    {
        var settings = CurrentSettings();
        var (from, to, label) = ResolveScope();
        ScopeLabel = label;

        var data = from is null || to is null
            ? _entries.GetAll()
            : _entries.GetRange(from.Value, to.Value);

        (DateOnly, DateOnly)? holidayScope = from is { } f && to is { } t ? (f, t)
            : data.Count > 0 ? (data[0].Date, data[^1].Date) : null;

        var result = SalaryCalculator.Estimate(data, settings, AppliedPrimes.Select(v => v.Model), holidayScope);

        NormalHoursText = Fmt.H(result.NormalHours);
        NormalPayText = Fmt.Money(result.NormalPay, settings.Currency);
        OvertimeHoursText = Fmt.H(result.OvertimeHours);
        OvertimePayText = Fmt.Money(result.OvertimePay, settings.Currency);
        PrimesText = Fmt.Money(result.PrimesTotal, settings.Currency);
        GrossText = Fmt.Money(result.Gross, settings.Currency);

        ShowPrimesLine = result.PrimesTotal != 0;
        ShowIfm = settings.ApplyEndOfMissionBonus;
        ShowIcp = settings.ApplyPaidLeaveBonus;
        IfmText = Fmt.Money(result.EndOfMissionBonus, settings.Currency);
        IcpText = Fmt.Money(result.PaidLeaveBonus, settings.Currency);
        TotalText = Fmt.Money(result.Total, settings.Currency);

        RaiseAll();
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
