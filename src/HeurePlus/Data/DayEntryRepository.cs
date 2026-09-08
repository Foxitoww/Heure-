using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.Data;

public sealed class DayEntryRepository
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH\\:mm";

    private readonly AppDatabase _db;
    private readonly AppEvents _events;

    public DayEntryRepository(AppDatabase db, AppEvents events)
    {
        _db = db;
        _events = events;
    }

    public DayEntry? Get(DateOnly date)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DayEntries WHERE Date = $d";
        command.Parameters.AddWithValue("$d", date.ToString(DateFormat, CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public List<DayEntry> GetRange(DateOnly from, DateOnly to)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DayEntries WHERE Date >= $a AND Date <= $b ORDER BY Date";
        command.Parameters.AddWithValue("$a", from.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$b", to.ToString(DateFormat, CultureInfo.InvariantCulture));
        return ReadAll(command);
    }

    public List<DayEntry> GetMonth(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return GetRange(first, first.AddMonths(1).AddDays(-1));
    }

    public List<DayEntry> GetAll()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM DayEntries ORDER BY Date";
        return ReadAll(command);
    }

    public (DateOnly Min, DateOnly Max)? GetDateBounds()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MIN(Date), MAX(Date) FROM DayEntries";
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0)) return null;
        return (ParseDate(reader.GetString(0)), ParseDate(reader.GetString(1)));
    }

    public void Save(DayEntry entry, bool notify = true)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = UpsertSql;
        Bind(command, entry);
        command.ExecuteNonQuery();
        if (notify) _events.RaiseEntriesChanged();
    }

    public void SaveMany(IEnumerable<DayEntry> entries)
    {
        using var connection = _db.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var entry in entries)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = UpsertSql;
            Bind(command, entry);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
        _events.RaiseEntriesChanged();
    }

    public void Delete(DateOnly date, bool notify = true)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DayEntries WHERE Date = $d";
        command.Parameters.AddWithValue("$d", date.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        if (notify) _events.RaiseEntriesChanged();
    }

    public void DeleteRange(DateOnly from, DateOnly to, bool notify = true)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DayEntries WHERE Date >= $a AND Date <= $b";
        command.Parameters.AddWithValue("$a", from.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$b", to.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        if (notify) _events.RaiseEntriesChanged();
    }

    private const string UpsertSql = """
        INSERT INTO DayEntries
            (Date, Status, StartTime, EndTime, BreakMinutes, NormalHours, OvertimeHours, HourlyRateOverride, Note, UpdatedAt)
        VALUES
            ($date, $status, $start, $end, $break, $normal, $ot, $rate, $note, $updated)
        ON CONFLICT(Date) DO UPDATE SET
            Status             = excluded.Status,
            StartTime          = excluded.StartTime,
            EndTime            = excluded.EndTime,
            BreakMinutes       = excluded.BreakMinutes,
            NormalHours        = excluded.NormalHours,
            OvertimeHours      = excluded.OvertimeHours,
            HourlyRateOverride = excluded.HourlyRateOverride,
            Note               = excluded.Note,
            UpdatedAt          = excluded.UpdatedAt;
        """;

    private static void Bind(SqliteCommand command, DayEntry e)
    {
        command.Parameters.AddWithValue("$date", e.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$status", (int)e.Status);
        command.Parameters.AddWithValue("$start", (object?)e.StartTime?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$end", (object?)e.EndTime?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? DBNull.Value);
        command.Parameters.AddWithValue("$break", e.BreakMinutes);
        command.Parameters.AddWithValue("$normal", e.NormalHours);
        command.Parameters.AddWithValue("$ot", e.OvertimeHours);
        command.Parameters.AddWithValue("$rate", (object?)e.HourlyRateOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("$note", e.Note ?? string.Empty);
        command.Parameters.AddWithValue("$updated", e.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    private static List<DayEntry> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var list = new List<DayEntry>();
        while (reader.Read()) list.Add(Map(reader));
        return list;
    }

    private static DayEntry Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        Date = ParseDate(r.GetString(r.GetOrdinal("Date"))),
        Status = (DayStatus)r.GetInt32(r.GetOrdinal("Status")),
        StartTime = ReadTime(r, "StartTime"),
        EndTime = ReadTime(r, "EndTime"),
        BreakMinutes = r.GetInt32(r.GetOrdinal("BreakMinutes")),
        NormalHours = r.GetDouble(r.GetOrdinal("NormalHours")),
        OvertimeHours = r.GetDouble(r.GetOrdinal("OvertimeHours")),
        HourlyRateOverride = r.IsDBNull(r.GetOrdinal("HourlyRateOverride"))
            ? null
            : r.GetDouble(r.GetOrdinal("HourlyRateOverride")),
        Note = r.GetString(r.GetOrdinal("Note")),
        UpdatedAt = DateTime.TryParse(r.GetString(r.GetOrdinal("UpdatedAt")), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now
    };

    private static DateOnly ParseDate(string s) =>
        DateOnly.ParseExact(s, DateFormat, CultureInfo.InvariantCulture);

    private static TimeOnly? ReadTime(SqliteDataReader r, string column)
    {
        int i = r.GetOrdinal(column);
        if (r.IsDBNull(i)) return null;
        return TimeOnly.TryParseExact(r.GetString(i), "HH\\:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var t) ? t : null;
    }
}
