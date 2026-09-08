using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.Data;

/// <summary>Stockage clé/valeur des réglages, avec accès typés.</summary>
public sealed class SettingsRepository
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly AppDatabase _db;
    private readonly AppEvents _events;

    public SettingsRepository(AppDatabase db, AppEvents events)
    {
        _db = db;
        _events = events;
    }

    // ---------- Salaire ----------

    public SalarySettings LoadSalary() => new()
    {
        HourlyRate = GetDouble("salary.rate", 12.00),
        OvertimeMultiplier = GetDouble("salary.otMultiplier", 1.25),
        ApplyEndOfMissionBonus = GetBool("salary.ifm.enabled", true),
        EndOfMissionRate = GetDouble("salary.ifm.rate", 0.10),
        ApplyPaidLeaveBonus = GetBool("salary.icp.enabled", true),
        PaidLeaveRate = GetDouble("salary.icp.rate", 0.10),
        WeeklyHours = GetDouble("salary.weeklyHours", 35),
        SmicHourly = GetDouble("salary.smic.hourly", 11.88),
        SmicCoefficient = GetDouble("salary.smic.coef", 1.0),
        Currency = GetString("app.currency") ?? "€"
    };

    public void SaveSalary(SalarySettings s)
    {
        SetMany(new Dictionary<string, string>
        {
            ["salary.rate"] = s.HourlyRate.ToString(Inv),
            ["salary.otMultiplier"] = s.OvertimeMultiplier.ToString(Inv),
            ["salary.ifm.enabled"] = Bool(s.ApplyEndOfMissionBonus),
            ["salary.ifm.rate"] = s.EndOfMissionRate.ToString(Inv),
            ["salary.icp.enabled"] = Bool(s.ApplyPaidLeaveBonus),
            ["salary.icp.rate"] = s.PaidLeaveRate.ToString(Inv),
            ["salary.weeklyHours"] = s.WeeklyHours.ToString(Inv),
            ["salary.smic.hourly"] = s.SmicHourly.ToString(Inv),
            ["salary.smic.coef"] = s.SmicCoefficient.ToString(Inv),
            ["app.currency"] = s.Currency
        });
        _events.RaiseSettingsChanged();
    }

    // ---------- Primes ----------

    public List<AppliedPrime> LoadPrimes()
    {
        var json = GetString("salary.primes");
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<AppliedPrime>>(json) ?? new(); }
        catch { return new(); }
    }

    public void SavePrimes(IEnumerable<AppliedPrime> primes)
    {
        SetMany(new Dictionary<string, string>
        {
            ["salary.primes"] = JsonSerializer.Serialize(primes)
        });
        _events.RaiseSettingsChanged();
    }

    // ---------- Application ----------

    public AppSettings LoadApp() => new()
    {
        // Thème sombre par défaut ; on ne repasse en clair que si l'utilisateur l'a explicitement choisi.
        Theme = GetString("app.theme") == nameof(AppTheme.Light) ? AppTheme.Light : AppTheme.Dark,
        FirstDayOfWeek = GetString("app.firstDayOfWeek") == nameof(DayOfWeek.Sunday)
            ? DayOfWeek.Sunday
            : DayOfWeek.Monday,
        LastBackupFolder = GetString("app.backupFolder"),
        LastExportFolder = GetString("app.exportFolder")
    };

    public void SaveApp(AppSettings a)
    {
        SetMany(new Dictionary<string, string>
        {
            ["app.theme"] = a.Theme.ToString(),
            ["app.firstDayOfWeek"] = a.FirstDayOfWeek.ToString(),
            ["app.backupFolder"] = a.LastBackupFolder ?? string.Empty,
            ["app.exportFolder"] = a.LastExportFolder ?? string.Empty
        });
        _events.RaiseSettingsChanged();
    }

    // ---------- Primitives ----------

    private string? GetString(string key)
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $k";
        command.Parameters.AddWithValue("$k", key);
        var value = command.ExecuteScalar() as string;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private double GetDouble(string key, double fallback) =>
        double.TryParse(GetString(key), NumberStyles.Any, Inv, out var v) ? v : fallback;

    private bool GetBool(string key, bool fallback)
    {
        var s = GetString(key);
        if (s is null) return fallback;
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private void SetMany(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        using var connection = _db.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var pair in pairs)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO Settings (Key, Value) VALUES ($k, $v) " +
                "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
            command.Parameters.AddWithValue("$k", pair.Key);
            command.Parameters.AddWithValue("$v", pair.Value ?? string.Empty);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static string Bool(bool value) => value ? "1" : "0";
}
