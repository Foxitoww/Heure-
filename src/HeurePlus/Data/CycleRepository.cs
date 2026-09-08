using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using HeurePlus.Models;

namespace HeurePlus.Data;

public sealed class CycleRepository
{
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly AppDatabase _db;

    public CycleRepository(AppDatabase db) => _db = db;

    /// <summary>Insère ou met à jour le cycle. Retourne son Id.</summary>
    public long Save(CyclePlan plan)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();

        string steps = JsonSerializer.Serialize(plan.Steps, Json);

        if (plan.Id > 0)
        {
            command.CommandText = """
                UPDATE Cycles SET
                    StartDate = $start, EndDate = $end, IncludeWeekends = $we,
                    HourlyRateOverride = $rate, Note = $note, StepsJson = $steps, UpdatedAt = $updated
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", plan.Id);
        }
        else
        {
            command.CommandText = """
                INSERT INTO Cycles (StartDate, EndDate, IncludeWeekends, HourlyRateOverride, Note, StepsJson, UpdatedAt)
                VALUES ($start, $end, $we, $rate, $note, $steps, $updated);
                SELECT last_insert_rowid();
                """;
        }

        command.Parameters.AddWithValue("$start", plan.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", plan.EndDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$we", plan.IncludeWeekends ? 1 : 0);
        command.Parameters.AddWithValue("$rate", (object?)plan.HourlyRateOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("$note", plan.Note ?? string.Empty);
        command.Parameters.AddWithValue("$steps", steps);
        command.Parameters.AddWithValue("$updated", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));

        if (plan.Id > 0)
        {
            command.ExecuteNonQuery();
            return plan.Id;
        }

        return Convert.ToInt64(command.ExecuteScalar() ?? 0L, CultureInfo.InvariantCulture);
    }

    public List<CyclePlan> GetAll()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Cycles ORDER BY StartDate DESC";
        return ReadAll(command);
    }

    /// <summary>Le cycle dont la plage contient la date (le plus récent si plusieurs).</summary>
    public CyclePlan? GetForDate(DateOnly date)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT * FROM Cycles WHERE StartDate <= $d AND EndDate >= $d ORDER BY UpdatedAt DESC LIMIT 1";
        command.Parameters.AddWithValue("$d", date.ToString(DateFormat, CultureInfo.InvariantCulture));
        var list = ReadAll(command);
        return list.Count > 0 ? list[0] : null;
    }

    public void Delete(long id)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cycles WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static List<CyclePlan> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var list = new List<CyclePlan>();
        while (reader.Read())
        {
            List<CyclePlanStep> steps;
            try
            {
                steps = JsonSerializer.Deserialize<List<CyclePlanStep>>(
                    reader.GetString(reader.GetOrdinal("StepsJson"))) ?? new();
            }
            catch
            {
                steps = new();
            }

            list.Add(new CyclePlan
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                StartDate = DateOnly.ParseExact(reader.GetString(reader.GetOrdinal("StartDate")), DateFormat, CultureInfo.InvariantCulture),
                EndDate = DateOnly.ParseExact(reader.GetString(reader.GetOrdinal("EndDate")), DateFormat, CultureInfo.InvariantCulture),
                IncludeWeekends = reader.GetInt32(reader.GetOrdinal("IncludeWeekends")) != 0,
                HourlyRateOverride = reader.IsDBNull(reader.GetOrdinal("HourlyRateOverride"))
                    ? null : reader.GetDouble(reader.GetOrdinal("HourlyRateOverride")),
                Note = reader.GetString(reader.GetOrdinal("Note")),
                Steps = steps,
                UpdatedAt = DateTime.TryParse(reader.GetString(reader.GetOrdinal("UpdatedAt")),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now
            });
        }
        return list;
    }
}
