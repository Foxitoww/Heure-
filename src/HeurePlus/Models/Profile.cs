using System;
using System.Text.Json.Serialization;

namespace HeurePlus.Models;

/// <summary>
/// Un profil = une personne du foyer. Chaque profil possède sa propre base de
/// données (<c>…\HeurePlus\profiles\{Id}\heureplus.db</c>) : heures, cycles,
/// salaire, primes, historique et thème lui sont propres. Seule la liste des
/// profils (<c>profiles.json</c>) est commune, et l'ouverture d'un profil exige
/// son identifiant + mot de passe.
/// </summary>
public sealed class Profile
{
    /// <summary>Identifiant technique stable ; sert aussi de nom de dossier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>Nom affiché sur la carte de profil.</summary>
    public string Name { get; set; } = "Moi";

    /// <summary>Identifiant de connexion (comparé sans tenir compte de la casse).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Couleur de la pastille, format <c>#RRGGBB</c>.</summary>
    public string ColorHex { get; set; } = "#3A7DDA";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

    // ---------- Mot de passe (PBKDF2, voir PasswordHasher) ----------

    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }

    /// <summary>
    /// Sel (base64, 16 octets) pour dériver la clé de chiffrement SQLCipher de la
    /// base à partir du mot de passe. Présent ⇒ la base du profil est chiffrée.
    /// Distinct de <see cref="PasswordSalt"/> : la clé de base et le hash de
    /// vérification ne doivent jamais être la même valeur.
    /// </summary>
    public string? DbKdfSalt { get; set; }

    [JsonIgnore]
    public bool IsEncrypted => !string.IsNullOrEmpty(DbKdfSalt);

    /// <summary>
    /// Faux uniquement pour un profil hérité de la version mono-utilisateur,
    /// tant que la personne ne s'est pas défini d'identifiants au 1er lancement.
    /// </summary>
    [JsonIgnore]
    public bool HasCredentials =>
        !string.IsNullOrEmpty(Username)
        && !string.IsNullOrEmpty(PasswordHash)
        && !string.IsNullOrEmpty(PasswordSalt);

    /// <summary>Initiale affichée dans la pastille et les cartes de profil.</summary>
    [JsonIgnore]
    public string Initial =>
        string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpperInvariant();
}
