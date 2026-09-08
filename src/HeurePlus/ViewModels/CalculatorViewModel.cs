using System;
using System.Globalization;
using HeurePlus.Infrastructure;

namespace HeurePlus.ViewModels;

/// <summary>Calculatrice classique + conversions heures / minutes.</summary>
public sealed class CalculatorViewModel : ObservableObject
{
    private static readonly CultureInfo Ui = Fmt.Fr;
    private static readonly string Sep = Ui.NumberFormat.NumberDecimalSeparator;

    public CalculatorViewModel()
    {
        KeyCommand = new RelayCommand(p => Key(p?.ToString() ?? string.Empty));
        RecomputeConversions();
    }

    public RelayCommand KeyCommand { get; }

    // ================= Calculatrice =================

    private string _display = "0";
    public string Display { get => _display; private set => SetProperty(ref _display, value); }

    private string _history = string.Empty;
    public string History { get => _history; private set => SetProperty(ref _history, value); }

    private double _accumulator;
    private string? _pendingOp;
    private bool _startNewEntry = true;
    private bool _hasPending;

    private double CurrentValue =>
        double.TryParse(_display, NumberStyles.Any, Ui, out var v) ? v : 0;

    public void Key(string key)
    {
        switch (key)
        {
            case "C":
                _accumulator = 0; _pendingOp = null; _hasPending = false; _startNewEntry = true;
                Display = "0"; History = string.Empty;
                break;

            case "CE":
                Display = "0"; _startNewEntry = true;
                break;

            case "back":
                if (!_startNewEntry && _display.Length > 1 && !(_display.Length == 2 && _display[0] == '-'))
                    Display = _display[..^1];
                else { Display = "0"; _startNewEntry = true; }
                break;

            case "+/-":
                if (_display != "0")
                    Display = _display.StartsWith('-') ? _display[1..] : "-" + _display;
                break;

            case "%":
                Display = Trim(CurrentValue / 100.0);
                _startNewEntry = true;
                break;

            case ".":
            case ",":
                if (_startNewEntry) { Display = "0" + Sep; _startNewEntry = false; }
                else if (!_display.Contains(Sep)) Display += Sep;
                break;

            case "+":
            case "-":
            case "×":
            case "÷":
                ApplyPending();
                _pendingOp = key;
                _hasPending = true;
                _startNewEntry = true;
                History = $"{Trim(_accumulator)} {key}";
                break;

            case "=":
                if (_hasPending)
                {
                    History = $"{Trim(_accumulator)} {_pendingOp} {_display} =";
                    ApplyPending();
                    _pendingOp = null;
                    _hasPending = false;
                    _startNewEntry = true;
                }
                break;

            default: // chiffre
                if (key.Length == 1 && char.IsDigit(key[0]))
                {
                    if (_startNewEntry) { Display = key; _startNewEntry = false; }
                    else Display = _display == "0" ? key : _display + key;
                }
                break;
        }
    }

    private void ApplyPending()
    {
        double rhs = CurrentValue;
        if (!_hasPending || _pendingOp is null)
        {
            _accumulator = rhs;
            return;
        }

        _accumulator = _pendingOp switch
        {
            "+" => _accumulator + rhs,
            "-" => _accumulator - rhs,
            "×" => _accumulator * rhs,
            "÷" => rhs == 0 ? double.NaN : _accumulator / rhs,
            _ => rhs
        };

        Display = double.IsNaN(_accumulator) || double.IsInfinity(_accumulator)
            ? "Erreur"
            : Trim(_accumulator);
    }

    private static string Trim(double value) =>
        value.ToString("0.##########", Ui);

    // ================= Conversions =================

    private string _decimalHoursInput = "7,5";
    public string DecimalHoursInput
    {
        get => _decimalHoursInput;
        set { if (SetProperty(ref _decimalHoursInput, value)) RecomputeConversions(); }
    }
    public string DecimalHoursResult { get; private set; } = "";

    private int _hmHours = 7;
    public int HmHours { get => _hmHours; set { if (SetProperty(ref _hmHours, value)) RecomputeConversions(); } }

    private int _hmMinutes = 30;
    public int HmMinutes { get => _hmMinutes; set { if (SetProperty(ref _hmMinutes, value)) RecomputeConversions(); } }

    public string HmResult { get; private set; } = "";

    private string _durationA = "08:15";
    public string DurationA { get => _durationA; set { if (SetProperty(ref _durationA, value)) RecomputeConversions(); } }

    private string _durationB = "01:30";
    public string DurationB { get => _durationB; set { if (SetProperty(ref _durationB, value)) RecomputeConversions(); } }

    private bool _durationAdd = true;
    public bool DurationAdd { get => _durationAdd; set { if (SetProperty(ref _durationAdd, value)) RecomputeConversions(); } }

    public string DurationResult { get; private set; } = "";

    private void RecomputeConversions()
    {
        // décimal -> h:mm
        var dec = Fmt.ParseDuration(_decimalHoursInput);
        DecimalHoursResult = dec is null ? "—" : Fmt.H(dec.Value);

        // h:mm -> décimal
        double hm = Fmt.ToDecimal(_hmHours, Math.Clamp(_hmMinutes, 0, 999));
        HmResult = hm.ToString("0.00##", Ui) + " h";

        // addition / soustraction de durées
        var a = Fmt.ParseDuration(_durationA);
        var b = Fmt.ParseDuration(_durationB);
        if (a is null || b is null)
        {
            DurationResult = "—";
        }
        else
        {
            double total = _durationAdd ? a.Value + b.Value : a.Value - b.Value;
            DurationResult = $"{Fmt.H(total)}   ({total.ToString("0.00##", Ui)} h)";
        }

        OnPropertyChanged(nameof(DecimalHoursResult));
        OnPropertyChanged(nameof(HmResult));
        OnPropertyChanged(nameof(DurationResult));
    }
}
