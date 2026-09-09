using System;
using System.IO;
using Microsoft.Data.Sqlite;
using HeurePlus.Data;

namespace HeurePlus.Services;

/// <summary>Sauvegarde et restauration du fichier de base de données (chiffré).</summary>
public sealed class BackupService
{
    private readonly AppDatabase _db;

    public BackupService(AppDatabase db) => _db = db;

    /// <summary>Copie la base dans le dossier choisi, horodatée. Retourne le chemin créé.</summary>
    public string Backup(string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        string fileName = $"heureplus-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        string destination = Path.Combine(destinationFolder, fileName);

        SqliteConnection.ClearAllPools();
        File.Copy(_db.DbPath, destination, overwrite: true);
        return destination;
    }

    /// <summary>Remplace la base courante par une sauvegarde. Redémarrage conseillé ensuite.</summary>
    public void Restore(string backupFile)
    {
        if (!File.Exists(backupFile))
            throw new FileNotFoundException("Sauvegarde introuvable.", backupFile);

        // La sauvegarde est chiffrée : elle n'est exploitable qu'avec la clé de CE
        // profil. Un fichier d'un autre profil (ou corrompu) est refusé ici plutôt
        // que d'écraser la base par un contenu illisible.
        if (!_db.CanUseFile(backupFile))
            throw new InvalidDataException(
                "Ce fichier n'est pas une sauvegarde de ce profil (ou il est illisible).");

        SqliteConnection.ClearAllPools();
        File.Copy(backupFile, _db.DbPath, overwrite: true);
    }
}
