using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HeurePlus.Data;

/// <summary>
/// Ouvre la base SQLite locale (chiffrée SQLCipher quand une clé est fournie) et
/// crée le schéma au besoin. La clé brute AES-256 reste en mémoire pour la
/// session et est appliquée via <c>PRAGMA key = "x'…'"</c> à chaque connexion.
/// </summary>
public sealed class AppDatabase
{
    private readonly string _connectionString;
    private string? _keyHex;

    public string DbPath { get; }

    /// <summary>Vrai si la base est ouverte avec une clé de chiffrement.</summary>
    public bool Encrypted => _keyHex is not null;

    public AppDatabase(string dbPath, byte[]? key = null)
    {
        DbPath = dbPath;
        _keyHex = key is null ? null : Convert.ToHexString(key);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            ForeignKeys = true
        }.ToString();

        Initialize();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (_keyHex is not null)
        {
            using var key = connection.CreateCommand();
            key.CommandText = $"PRAGMA key = \"x'{_keyHex}'\";";
            key.ExecuteNonQuery();
        }

        return connection;
    }

    /// <summary>Change la clé de chiffrement de la base (changement de mot de passe).</summary>
    public void Rekey(byte[] newKey)
    {
        string newHex = Convert.ToHexString(newKey);
        using (var connection = Open())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA rekey = \"x'{newHex}'\";";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        _keyHex = newHex;
    }

    /// <summary>
    /// Teste si un fichier de base peut être ouvert avec la clé courante
    /// (sert à valider une sauvegarde avant restauration).
    /// </summary>
    public bool CanUseFile(string path)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            using var connection = new SqliteConnection(cs);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = _keyHex is not null
                ? $"PRAGMA key = \"x'{_keyHex}'\"; SELECT count(*) FROM sqlite_master;"
                : "SELECT count(*) FROM sqlite_master;";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DayEntries (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                Date               TEXT    NOT NULL UNIQUE,
                Status             INTEGER NOT NULL DEFAULT 0,
                StartTime          TEXT    NULL,
                EndTime            TEXT    NULL,
                BreakMinutes       INTEGER NOT NULL DEFAULT 0,
                NormalHours        REAL    NOT NULL DEFAULT 0,
                OvertimeHours      REAL    NOT NULL DEFAULT 0,
                HourlyRateOverride REAL    NULL,
                Note               TEXT    NOT NULL DEFAULT '',
                UpdatedAt          TEXT    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_DayEntries_Date ON DayEntries(Date);

            CREATE TABLE IF NOT EXISTS Settings (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ActivityLog (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp   TEXT    NOT NULL,
                Category    INTEGER NOT NULL,
                Description TEXT    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ActivityLog_Timestamp ON ActivityLog(Timestamp DESC);

            CREATE TABLE IF NOT EXISTS Cycles (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                StartDate          TEXT    NOT NULL,
                EndDate            TEXT    NOT NULL,
                IncludeWeekends    INTEGER NOT NULL DEFAULT 0,
                HourlyRateOverride REAL    NULL,
                Note               TEXT    NOT NULL DEFAULT '',
                StepsJson          TEXT    NOT NULL,
                UpdatedAt          TEXT    NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
