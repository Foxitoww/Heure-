using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using HeurePlus.Data;

namespace HeurePlus.Services;

/// <summary>Sauvegarde et restauration du fichier de base de données.</summary>
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

        if (!LooksLikeSqlite(backupFile))
            throw new InvalidDataException("Le fichier sélectionné n'est pas une base SQLite valide.");

        SqliteConnection.ClearAllPools();
        File.Copy(backupFile, _db.DbPath, overwrite: true);
    }

    private static bool LooksLikeSqlite(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[16];
            if (stream.Read(header) < 16) return false;
            return Encoding.ASCII.GetString(header).StartsWith("SQLite format 3", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
