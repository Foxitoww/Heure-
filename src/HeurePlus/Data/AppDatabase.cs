using System.IO;
using Microsoft.Data.Sqlite;

namespace HeurePlus.Data;

/// <summary>Ouvre la base SQLite locale et crée le schéma au besoin.</summary>
public sealed class AppDatabase
{
    private readonly string _connectionString;

    public string DbPath { get; }

    public AppDatabase(string dbPath)
    {
        DbPath = dbPath;
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
        return connection;
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
