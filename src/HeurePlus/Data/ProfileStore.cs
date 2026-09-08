using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HeurePlus.Models;

namespace HeurePlus.Data;

/// <summary>
/// Liste des profils du foyer, persistée dans
/// <c>%LOCALAPPDATA%\HeurePlus\profiles.json</c>. Chaque profil a sa base à
/// <c>…\HeurePlus\profiles\{Id}\heureplus.db</c> ; ce store ne gère que le
/// registre commun (identités, couleurs, mot de passe haché, profil mémorisé).
/// </summary>
public sealed class ProfileStore
{
    /// <summary>Palette d'accent proposée aux nouveaux profils (couleurs de statut de l'app).</summary>
    public static readonly string[] Palette =
        { "#3A7DDA", "#2FA84F", "#8B5CF6", "#EAB308", "#E5484D", "#0EA5E9" };

    private sealed class Doc
    {
        public string? RememberedProfileId { get; set; }
        public string? LastActiveId { get; set; }
        public List<Profile> Profiles { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _root;
    private readonly string _jsonPath;
    private readonly Doc _doc;

    public ProfileStore(string rootDir)
    {
        _root = rootDir;
        _jsonPath = Path.Combine(_root, "profiles.json");
        Directory.CreateDirectory(_root);
        _doc = Load();
    }

    public IReadOnlyList<Profile> All => _doc.Profiles;

    /// <summary>Profil à ouvrir sans repasser par l'écran de sélection (case « se souvenir »).</summary>
    public string? RememberedProfileId
    {
        get => _doc.RememberedProfileId;
        set { _doc.RememberedProfileId = value; Save(); }
    }

    public string? LastActiveId
    {
        get => _doc.LastActiveId;
        set { _doc.LastActiveId = value; Save(); }
    }

    public Profile? Get(string? id) =>
        id is null ? null : _doc.Profiles.FirstOrDefault(p => p.Id == id);

    public Profile? GetByUsername(string username) =>
        _doc.Profiles.FirstOrDefault(p =>
            string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));

    /// <summary>Chemin de la base d'un profil ; le dossier est créé au besoin.</summary>
    public string DbPathFor(string id)
    {
        string dir = Path.Combine(_root, "profiles", id);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "heureplus.db");
    }

    /// <summary>Couleur d'accent suggérée pour le prochain profil.</summary>
    public string SuggestColor() => Palette[_doc.Profiles.Count % Palette.Length];

    public void Add(Profile profile)
    {
        _doc.Profiles.Add(profile);
        Save();
    }

    public void Update(Profile profile)
    {
        int i = _doc.Profiles.FindIndex(p => p.Id == profile.Id);
        if (i >= 0) _doc.Profiles[i] = profile;
        Save();
    }

    /// <summary>Supprime le profil ET son dossier de données. Refuse le dernier profil.</summary>
    public void Delete(string id)
    {
        if (_doc.Profiles.Count <= 1)
            throw new InvalidOperationException("Impossible de supprimer le dernier profil.");

        _doc.Profiles.RemoveAll(p => p.Id == id);
        if (_doc.LastActiveId == id) _doc.LastActiveId = null;
        if (_doc.RememberedProfileId == id) _doc.RememberedProfileId = null;

        try
        {
            string dir = Path.Combine(_root, "profiles", id);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Dossier verrouillé : on le laisse, il sera orphelin et inoffensif.
        }

        Save();
    }

    /// <summary>
    /// Premier lancement multi-profils : s'il n'y a encore aucun profil mais qu'une
    /// base mono-utilisateur héritée existe (<c>…\HeurePlus\heureplus.db</c>, version 0.2.0),
    /// on crée un profil « Moi » et on lui rattache cette base. Ce profil démarre
    /// SANS identifiants — l'écran de sélection détecte <see cref="Profile.HasCredentials"/>
    /// à false et demande de les définir au premier accès.
    /// </summary>
    public void EnsureMigratedLegacy()
    {
        if (_doc.Profiles.Count > 0) return;

        string legacyDb = Path.Combine(_root, "heureplus.db");
        if (!File.Exists(legacyDb)) return; // nouvelle installation : rien à migrer

        var profile = new Profile { Name = "Moi", ColorHex = Palette[0] };
        string targetDb = DbPathFor(profile.Id); // crée …\profiles\{id}\ (le fichier n'existe pas encore)

        try
        {
            // Copie : la base + ses éventuels journaux WAL/SHM non encore fusionnés.
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                string src = legacyDb + suffix;
                if (File.Exists(src)) File.Copy(src, targetDb + suffix, overwrite: true);
            }

            // L'original est retiré du chemin hérité (sinon la migration se
            // redéclencherait) mais conservé sous .bak, récupérable à la main.
            string retired = legacyDb + ".premultiprofile.bak";
            if (File.Exists(retired)) File.Delete(retired);
            File.Move(legacyDb, retired);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(legacyDb + suffix)) File.Delete(legacyDb + suffix);
        }
        catch (Exception ex)
        {
            // Échec de copie (fichier verrouillé, disque plein…) : on n'a rien
            // écrasé, l'original reste en place. Le profil démarrera sur une base
            // vide dont le schéma se recrée tout seul.
            System.Diagnostics.Debug.WriteLine($"[ProfileStore] Migration héritée impossible : {ex}");
        }

        _doc.Profiles.Add(profile);
        _doc.LastActiveId = profile.Id;
        Save();
    }

    private Doc Load()
    {
        try
        {
            if (File.Exists(_jsonPath))
                return JsonSerializer.Deserialize<Doc>(File.ReadAllText(_jsonPath)) ?? new Doc();
        }
        catch
        {
            // profiles.json illisible : on repart d'un registre vide plutôt que de planter.
        }
        return new Doc();
    }

    private void Save() => File.WriteAllText(_jsonPath, JsonSerializer.Serialize(_doc, Json));
}
