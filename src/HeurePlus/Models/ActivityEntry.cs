using System;

namespace HeurePlus.Models;

/// <summary>Catégorie d'une action journalisée.</summary>
public enum ActivityCategory
{
    Saisie,
    Suppression,
    Periode,
    Salaire,
    Reglages,
    Sauvegarde,
    Export
}

/// <summary>Une ligne du journal d'activité (onglet Historique).</summary>
public sealed class ActivityEntry
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public ActivityCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CategoryLabel => Category switch
    {
        ActivityCategory.Saisie => "Saisie",
        ActivityCategory.Suppression => "Suppression",
        ActivityCategory.Periode => "Période",
        ActivityCategory.Salaire => "Salaire",
        ActivityCategory.Reglages => "Réglages",
        ActivityCategory.Sauvegarde => "Sauvegarde",
        ActivityCategory.Export => "Export",
        _ => Category.ToString()
    };
}
