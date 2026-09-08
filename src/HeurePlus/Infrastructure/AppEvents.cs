using System;

namespace HeurePlus.Infrastructure;

/// <summary>
/// Bus d'événements très simple pour synchroniser les onglets
/// (le calendrier écrit -> le tableau de bord et le salaire se rafraîchissent).
/// </summary>
public sealed class AppEvents
{
    /// <summary>Levé quand une saisie de jour est ajoutée, modifiée ou supprimée.</summary>
    public event Action? EntriesChanged;

    /// <summary>Levé quand les réglages (salaire, thème, devise…) changent.</summary>
    public event Action? SettingsChanged;

    /// <summary>Levé quand une action est ajoutée au journal d'activité.</summary>
    public event Action? ActivityLogged;

    public void RaiseEntriesChanged() => EntriesChanged?.Invoke();

    public void RaiseSettingsChanged() => SettingsChanged?.Invoke();

    public void RaiseActivityLogged() => ActivityLogged?.Invoke();
}
