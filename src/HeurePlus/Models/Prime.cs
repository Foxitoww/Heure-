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

        // Santé / médico-social (secteur hospitalier, EHPAD, social)
        new("Complément de traitement indiciaire (Ségur)", "Santé / médico-social", "Revalorisation Ségur de la santé : 49 points d'indice pour les agents hospitaliers et médico-sociaux.", PrimeUnit.Mensuel, 238),
        new("Revalorisation Ségur 2 (socio-éducatif)", "Santé / médico-social", "Extension du Ségur aux métiers socio-éducatifs et d'accompagnement.", PrimeUnit.Mensuel, 238),
        new("Prime Grand âge", "Santé / médico-social", "Personnels des EHPAD et services gériatriques.", PrimeUnit.Mensuel, 118),
        new("Prime de service (hospitalière)", "Santé / médico-social", "Jusqu'à 7,5 % du traitement brut, selon présence et notation.", PrimeUnit.PourcentBrut, 7.5),
        new("Prime spécifique / de sujétion soignants", "Santé / médico-social", "Aides-soignants, AMP, auxiliaires : sujétions particulières.", PrimeUnit.PourcentBrut, 10),
        new("Prime d'encadrement (cadres de santé)", "Santé / médico-social", "Fonctions d'encadrement soignant.", PrimeUnit.Mensuel, 91),
        new("Indemnité forfaitaire de nuit (hôpital)", "Santé / médico-social", "Heures de nuit dans la fonction publique hospitalière.", PrimeUnit.ParHeure, 1.10),
        new("Indemnité dimanches et jours fériés (hôpital)", "Santé / médico-social", "Par dimanche ou jour férié travaillé à l'hôpital.", PrimeUnit.ParJourTravaille, 48),
        new("Nouvelle bonification indiciaire (NBI)", "Santé / médico-social", "Points d'indice supplémentaires pour certaines fonctions.", PrimeUnit.Mensuel, 98),
        new("Prime de tenue / blanchissage (soignants)", "Santé / médico-social", "Entretien des tenues professionnelles.", PrimeUnit.Mensuel, 15),

        // Fonction publique (transversal)
        new("Indemnité de résidence", "Fonction publique", "Selon la zone d'affectation (0 %, 1 % ou 3 % du traitement).", PrimeUnit.PourcentBrut, 1),
        new("Supplément familial de traitement (SFT)", "Fonction publique", "Part fixe et part proportionnelle selon le nombre d'enfants.", PrimeUnit.Mensuel, 73),
        new("IFSE (RIFSEEP)", "Fonction publique", "Indemnité de fonctions, de sujétions et d'expertise.", PrimeUnit.Mensuel, 350),
        new("Complément indemnitaire annuel (CIA)", "Fonction publique", "Part variable du RIFSEEP liée à l'engagement et à la manière de servir.", PrimeUnit.Fixe, 700),
        new("Garantie individuelle du pouvoir d'achat (GIPA)", "Fonction publique", "Versée si le traitement a moins progressé que l'inflation sur 4 ans.", PrimeUnit.Fixe, 0),
        new("Indemnité compensatrice de CSG", "Fonction publique", "Compense la hausse de CSG de 2018.", PrimeUnit.PourcentBrut, 1.67),
        new("Indemnité de sujétions / de fonctions", "Fonction publique", "Contraintes particulières du poste.", PrimeUnit.Mensuel, 60),

        // Enseignement
        new("ISOE (enseignants du second degré)", "Enseignement", "Indemnité de suivi et d'orientation des élèves, part fixe.", PrimeUnit.Mensuel, 100),
        new("ISAE (professeurs des écoles)", "Enseignement", "Indemnité de suivi et d'accompagnement, premier degré.", PrimeUnit.Mensuel, 100),
        new("Prime d'attractivité (enseignants)", "Enseignement", "Début et milieu de carrière.", PrimeUnit.Mensuel, 100),
        new("Part fonctionnelle du Pacte enseignant", "Enseignement", "Missions complémentaires acceptées dans le cadre du Pacte.", PrimeUnit.Fixe, 1250),
        new("Indemnité REP", "Enseignement", "Affectation en réseau d'éducation prioritaire.", PrimeUnit.Mensuel, 144),
        new("Indemnité REP+", "Enseignement", "Affectation en REP renforcé.", PrimeUnit.Mensuel, 400),
        new("Heure supplémentaire année (HSA)", "Enseignement", "Heure assurée toute l'année en plus du service.", PrimeUnit.Mensuel, 70),
        new("Heure supplémentaire effective (HSE)", "Enseignement", "Heure d'enseignement ponctuelle.", PrimeUnit.ParHeure, 40),
        new("Indemnité de professeur principal", "Enseignement", "Fonction de professeur principal d'une classe.", PrimeUnit.Mensuel, 105),

        // Sécurité / Défense
        new("Indemnité de sujétions spéciales de police (ISSP)", "Sécurité / Défense", "Environ 27 % du traitement brut pour les policiers actifs.", PrimeUnit.PourcentBrut, 27),
        new("Prime de feu (sapeurs-pompiers)", "Sécurité / Défense", "25 % du traitement indiciaire brut des sapeurs-pompiers professionnels.", PrimeUnit.PourcentBrut, 25),
        new("Indemnité d'absence du domicile (militaires)", "Sécurité / Défense", "Missions et déplacements hors garnison.", PrimeUnit.ParJourTravaille, 20),
        new("Indemnité pour services aériens ou sous-marins", "Sécurité / Défense", "Personnels navigants et sous-mariniers.", PrimeUnit.Mensuel, 300),
        new("Surrémunération OPEX (opération extérieure)", "Sécurité / Défense", "Majoration de solde en opération extérieure.", PrimeUnit.PourcentBrut, 100),
        new("Indemnité de risque / d'intervention", "Sécurité / Défense", "Interventions à risque (police, gendarmerie, pénitentiaire).", PrimeUnit.Mensuel, 120),

        // BTP
        new("Indemnité de trajet (BTP)", "BTP", "Compense le temps de trajet vers le chantier, selon la zone.", PrimeUnit.ParJourTravaille, 9),
        new("Indemnité de transport (BTP)", "BTP", "Compense les frais de transport vers le chantier, selon la zone.", PrimeUnit.ParJourTravaille, 5),
        new("Indemnité de repas / panier (BTP)", "BTP", "Repas pris hors du domicile sur le chantier.", PrimeUnit.ParJourTravaille, 11.50),
        new("Indemnité de grand déplacement (BTP)", "BTP", "Découché quand le chantier est trop loin du domicile.", PrimeUnit.ParJourTravaille, 75),
        new("Indemnité d'intempéries", "BTP", "Arrêt de chantier pour cause d'intempéries.", PrimeUnit.ParJourTravaille, 60),
        new("Prime de congés payés BTP (caisse CIBTP)", "BTP", "Congés du BTP versés par la caisse, avec majoration.", PrimeUnit.PourcentBrut, 11.5),

        // Transport routier
        new("Frais de route / repas (routiers)", "Transport routier", "Repas et casse-croûte selon la convention transport.", PrimeUnit.ParJourTravaille, 15),
        new("Indemnité de découché (routiers)", "Transport routier", "Repos hors du domicile : repas et chambre.", PrimeUnit.ParJourTravaille, 70),
        new("Prime de roulage / au kilomètre", "Transport routier", "Calculée sur les kilomètres parcourus.", PrimeUnit.Mensuel, 150),
        new("Prime éco-conduite / qualité", "Transport routier", "Conduite économe, absence de sinistre.", PrimeUnit.Mensuel, 80),
        new("Prime de non-accident", "Transport routier", "Aucun accident responsable sur la période.", PrimeUnit.Mensuel, 60),
        new("Prime de manutention / livraison", "Transport routier", "Chargement, déchargement, livraisons multiples.", PrimeUnit.ParJourTravaille, 6),

        // Hôtellerie - restauration
        new("Avantage en nature nourriture (HCR)", "Hôtellerie-restauration", "Repas fournis, évalués forfaitairement par la convention HCR.", PrimeUnit.ParJourTravaille, 8),
        new("Prime de coupure (HCR)", "Hôtellerie-restauration", "Journée avec coupure entre les services.", PrimeUnit.ParJourTravaille, 10),
        new("Prime d'habillage (HCR)", "Hôtellerie-restauration", "Temps d'habillage et entretien de la tenue.", PrimeUnit.ParJourTravaille, 1.50),
        new("Part de service / pourboires", "Hôtellerie-restauration", "Part de service reversée au personnel en salle.", PrimeUnit.Mensuel, 200),

        // Industrie / travail posté
        new("Prime de poste (travail en équipes)", "Industrie / travail posté", "Organisation en 2x8, 3x8 ou 5x8.", PrimeUnit.ParJourTravaille, 12),
        new("Prime d'équipe / de relève", "Industrie / travail posté", "Passage de consignes entre équipes successives.", PrimeUnit.ParJourTravaille, 5),
        new("Prime de casse-croûte", "Industrie / travail posté", "Pause repas en poste continu.", PrimeUnit.ParJourTravaille, 6),
        new("Prime de feu (métallurgie)", "Industrie / travail posté", "Travail près des installations à haute température.", PrimeUnit.ParHeure, 0.90),
        new("Prime de feu continu / sujétion continue", "Industrie / travail posté", "Installations fonctionnant 24h/24, 7j/7.", PrimeUnit.Mensuel, 90),
        new("Prime de production / cadence", "Industrie / travail posté", "Objectifs de cadence ou de rendement de ligne.", PrimeUnit.Mensuel, 110),

        // Événements / divers
        new("Prime de cooptation / parrainage", "Événements / divers", "Recommandation d'un candidat finalement embauché.", PrimeUnit.Fixe, 500),
        new("Prime de médaille du travail", "Événements / divers", "Remise d'une médaille d'honneur du travail.", PrimeUnit.Fixe, 300),
        new("Prime de naissance / mariage (conventionnelle)", "Événements / divers", "Événement familial, si la convention collective la prévoit.", PrimeUnit.Fixe, 200),
        new("Prime de départ à la retraite", "Événements / divers", "Indemnité de départ, selon l'ancienneté.", PrimeUnit.Fixe, 1000),
        new("Prime exceptionnelle / discrétionnaire", "Événements / divers", "Versement ponctuel décidé par l'employeur.", PrimeUnit.Fixe, 0),
        new("Prime de rappel", "Événements / divers", "Salarié rappelé pendant un congé ou un repos.", PrimeUnit.Fixe, 80),
        new("Prime de bienvenue (welcome bonus)", "Événements / divers", "Versée à l'embauche sur des postes en tension.", PrimeUnit.Fixe, 0),
        new("Forfait mobilités durables", "Événements / divers", "Trajets domicile-travail à vélo ou en covoiturage.", PrimeUnit.Mensuel, 25),
    };
}
