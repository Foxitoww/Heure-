using System.Collections.Generic;

namespace HeurePlus.Models;

/// <summary>Mode de calcul d'une prime.</summary>
public enum PrimeUnit
{
    Mensuel,          // montant fixe par mois
    ParJourTravaille, // montant × nombre de jours travaillés
    ParHeure,         // montant × nombre d'heures
    PourcentBrut,     // pourcentage du brut (hors primes)
    Fixe              // montant unique sur la période
}

public static class PrimeUnitExtensions
{
    public static string Label(this PrimeUnit u) => u switch
    {
        PrimeUnit.Mensuel => "€ / mois",
        PrimeUnit.ParJourTravaille => "€ / jour travaillé",
        PrimeUnit.ParHeure => "€ / heure",
        PrimeUnit.PourcentBrut => "% du brut",
        PrimeUnit.Fixe => "€ (montant unique)",
        _ => u.ToString()
    };
}

/// <summary>Une prime du catalogue (référence).</summary>
public sealed record Prime(string Name, string Category, string Description, PrimeUnit Unit, double DefaultAmount);

/// <summary>Une prime ajoutée par l'utilisateur à son salaire (sérialisable).</summary>
public sealed class AppliedPrime
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public PrimeUnit Unit { get; set; }
    public double Amount { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Catalogue de primes courantes en France (montants indicatifs, à ajuster).
/// La prime de précarité (IFM) et l'indemnité de congés payés ont leurs propres
/// options dans l'onglet Salaire — ne pas les redoubler ici.
/// </summary>
public static class PrimeCatalog
{
    public static readonly IReadOnlyList<Prime> All = new List<Prime>
    {
        // Ancienneté / résultats
        new("Prime d'ancienneté", "Ancienneté", "Selon la convention collective, croît avec les années.", PrimeUnit.PourcentBrut, 3),
        new("Prime de 13e mois", "Ancienneté", "Un mois de salaire réparti sur l'année.", PrimeUnit.PourcentBrut, 8.33),
        new("Prime de vacances", "Ancienneté", "Versée avant les congés d'été.", PrimeUnit.Fixe, 500),
        new("Prime de fin d'année", "Ancienneté", "Versée en décembre.", PrimeUnit.Fixe, 300),
        new("Prime de bilan / résultats", "Ancienneté", "Selon les résultats de l'entreprise.", PrimeUnit.Fixe, 400),
        new("Prime de partage de la valeur (PPV)", "Ancienneté", "Ex-prime Macron, exonérée sous conditions.", PrimeUnit.Fixe, 0),

        // Conditions de travail
        new("Prime de panier (repas)", "Conditions", "Repas pris sur le lieu de travail.", PrimeUnit.ParJourTravaille, 7.30),
        new("Prime de transport", "Conditions", "Participation aux frais de trajet.", PrimeUnit.Mensuel, 50),
        new("Indemnité kilométrique", "Conditions", "Trajets avec véhicule personnel.", PrimeUnit.ParJourTravaille, 6),
        new("Prime de salissure", "Conditions", "Travaux salissants, entretien des vêtements.", PrimeUnit.Mensuel, 30),
        new("Prime d'outillage", "Conditions", "Usage d'outils personnels.", PrimeUnit.Mensuel, 20),
        new("Prime de douche / habillage", "Conditions", "Temps d'habillage et de douche.", PrimeUnit.ParJourTravaille, 1.20),
        new("Prime de froid", "Conditions", "Travail en chambre froide / extérieur.", PrimeUnit.ParHeure, 0.50),
        new("Prime de chaleur", "Conditions", "Exposition à de fortes températures.", PrimeUnit.ParHeure, 0.50),
        new("Prime de bruit", "Conditions", "Environnement bruyant.", PrimeUnit.ParHeure, 0.30),
        new("Prime de hauteur", "Conditions", "Travail en hauteur.", PrimeUnit.ParHeure, 0.80),
        new("Prime d'insalubrité", "Conditions", "Travaux insalubres.", PrimeUnit.ParHeure, 0.60),
        new("Prime de risque / danger", "Conditions", "Postes dangereux.", PrimeUnit.ParHeure, 1.00),

        // Horaires atypiques
        new("Prime de nuit", "Horaires", "Heures effectuées la nuit.", PrimeUnit.ParHeure, 1.50),
        new("Prime de dimanche", "Horaires", "Travail le dimanche.", PrimeUnit.ParJourTravaille, 25),
        new("Prime de jour férié", "Horaires", "Travail un jour férié.", PrimeUnit.ParJourTravaille, 35),
        new("Prime de coupure", "Horaires", "Journée avec coupure importante.", PrimeUnit.ParJourTravaille, 8),
        new("Prime d'astreinte", "Horaires", "Période d'astreinte à domicile.", PrimeUnit.ParJourTravaille, 25),

        // Responsabilité / compétences
        new("Prime de responsabilité", "Responsabilité", "Encadrement, responsabilités élargies.", PrimeUnit.Mensuel, 150),
        new("Prime de rendement / production", "Responsabilité", "Objectifs de production atteints.", PrimeUnit.Mensuel, 100),
        new("Prime d'objectif", "Responsabilité", "Atteinte d'objectifs individuels.", PrimeUnit.Mensuel, 100),
        new("Prime d'assiduité / présence", "Responsabilité", "Aucune absence sur la période.", PrimeUnit.Mensuel, 50),
        new("Prime de tutorat", "Responsabilité", "Encadrement d'un alternant / nouvel arrivant.", PrimeUnit.Mensuel, 60),
        new("Prime de technicité", "Responsabilité", "Compétence technique particulière.", PrimeUnit.Mensuel, 80),
        new("Prime de polyvalence", "Responsabilité", "Occupation de plusieurs postes.", PrimeUnit.Mensuel, 70),
        new("Prime de langue", "Responsabilité", "Usage professionnel d'une langue étrangère.", PrimeUnit.Mensuel, 50),
        new("Prime de caisse", "Responsabilité", "Tenue de caisse, responsabilité des fonds.", PrimeUnit.Mensuel, 30),

        // Mobilité
        new("Prime de déplacement", "Mobilité", "Déplacements professionnels réguliers.", PrimeUnit.ParJourTravaille, 40),
        new("Prime de grand déplacement", "Mobilité", "Découché, hébergement hors domicile.", PrimeUnit.ParJourTravaille, 70),
        new("Prime de mission", "Mobilité", "Mission spécifique confiée.", PrimeUnit.Mensuel, 120),
        new("Prime d'expatriation", "Mobilité", "Mission à l'étranger.", PrimeUnit.PourcentBrut, 10),
    };
}
