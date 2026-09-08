using System;

namespace HeurePlus.Models;

public enum AppTheme
{
    Light,
    Dark
}

/// <summary>Réglages généraux de l'application.</summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Monday;

    public string? LastBackupFolder { get; set; }

    public string? LastExportFolder { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
