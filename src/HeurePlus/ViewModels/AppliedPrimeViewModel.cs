using System;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

/// <summary>Une prime ajoutée par l'utilisateur, éditable dans l'onglet Primes.</summary>
public sealed class AppliedPrimeViewModel : ObservableObject
{
    private readonly Action _changed;

    public AppliedPrimeViewModel(AppliedPrime model, Action changed)
    {
        Model = model;
        _changed = changed;
        _amount = model.Amount;
        _enabled = model.Enabled;
    }

    public AppliedPrime Model { get; }

    public string Name => Model.Name;
    public string Category => Model.Category;
    public string UnitLabel => Model.Unit.Label();

    private double _amount;
    public double Amount
    {
        get => _amount;
        set
        {
            if (!SetProperty(ref _amount, value)) return;
            Model.Amount = value;
            _changed();
        }
    }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value)) return;
            Model.Enabled = value;
            _changed();
        }
    }
}
