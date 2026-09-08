using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.Data;

public sealed class ActivityLogRepository
{
    private readonly AppDatabase _db;
    private readonly AppEvents _events;

    public ActivityLogRepository(AppDatabase db, AppEvents events)
    {
        _db = db;
        _events = events;
    }

    public void Log(ActivityCategory category, string description)
    {
        try
        {
            using var connection = _db.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO ActivityLog (Timestamp, Category, Description) VALUES ($t, $c, $d)";
            command.Parameters.AddWithValue("$t", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$c", (int)category);
            command.Parameters.AddWithValue("$d", description ?? string.Empty);
            command.ExecuteNonQuery();
        }
        catch
        {
            // La journalisation ne doit jamais faire échouer une action.
        }

        _events.RaiseActivityLogged();
    }

    public List<ActivityEntry> GetRecent(int limit = 300)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Timestamp, Category, Description FROM ActivityLog ORDER BY Id DESC LIMIT $n";
        command.Parameters.AddWithValue("$n", limit);

        using var reader = command.ExecuteReader();
        var list = new List<ActivityEntry>();
        while (reader.Read())
        {
            list.Add(new ActivityEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.TryParse(reader.GetString(1), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now,
                Category = (ActivityCategory)reader.GetInt32(2),
                Description = reader.GetString(3)
            });
        }
        return list;
    }

    public int Count()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ActivityLog";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    public void Clear()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ActivityLog";
        command.ExecuteNonQuery();
        _events.RaiseActivityLogged();
    }
}
