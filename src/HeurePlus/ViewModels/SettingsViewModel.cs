using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;

namespace HeurePlus.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepo;
    private readonly BackupService _backup;
    private readonly ThemeManager _theme;
    private readonly DialogService _dialogs;
    private readonly DayEntryRepository _entries;
    private readonly ActivityLogRepository _activityLog;
    private readonly AppEvents _events;
    private readonly AppDatabase _db;
    private readonly ProfileStore _profiles;
    private readonly Profile _currentProfile;
    private readonly byte[] _sessionKey;
    private readonly Action _switchProfile;
    private readonly Func<ProfileEditViewModel, bool?> _showProfileEdit;
    private readonly UpdateService _update = new();

    private AppSettings _app;

    public SettingsViewModel(
        SettingsRepository settingsRepo,
        BackupService backup,
        ThemeManager theme,
        DialogService dialogs,
        DayEntryRepository entries,
        ActivityLogRepository activityLog,
        AppEvents events,
        AppDatabase db,
        ProfileStore profiles,
        Profile currentProfile,
        byte[] sessionKey,
        Action switchProfile,
        Func<ProfileEditViewModel, bool?> showProfileEdit)
    {
        _settingsRepo = settingsRepo;
        _backup = backup;
        _theme = theme;
        _dialogs = dialogs;
        _entries = entries;
        _activityLog = activityLog;
        _events = events;
        _db = db;
        _profiles = profiles;
        _currentProfile = currentProfile;
        _sessionKey = sessionKey;
        _switchProfile = switchProfile;
        _showProfileEdit = showProfileEdit;

        _app = _settingsRepo.LoadApp();
        _isDark = _app.Theme == AppTheme.Dark;
        _weekStartsMonday = _app.FirstDayOfWeek == DayOfWeek.Monday;
        _currency = _settingsRepo.LoadSalary().Currency;
        _exportMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        BackupNowCommand = new RelayCommand(_ => BackupNow());
        RestoreCommand = new RelayCommand(_ => Restore());
        OpenDbFolderCommand = new RelayCommand(_ => OpenFolder(Path.GetDirectoryName(_db.DbPath)));
        ExportPdfCommand = new RelayCommand(_ => Export(ExportKind.Pdf));
        ExportExcelCommand = new RelayCommand(_ => Export(ExportKind.Excel));
        CheckUpdateCommand = new RelayCommand(_ => _ = CheckUpdateAsync(), _ => !_updateBusy);
        ApplyUpdateCommand = new RelayCommand(_ => _ = ApplyUpdateAsync(), _ => !_updateBusy && _updateAvailable);

        _ = CheckUpdateOnStartupAsync();
    }

    public RelayCommand BackupNowCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand OpenDbFolderCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportExcelCommand { get; }

    // ---------- Profil ----------

    public string ProfileName => _currentProfile.Name;
    public string ProfileUsername => _currentProfile.Username;

    public bool RememberOnThisDevice
    {
        get => _profiles.RememberedProfileId == _currentProfile.Id;
        set
        {
            if (value) _profiles.RememberKey(_currentProfile.Id, _sessionKey);
            else _profiles.ForgetRemembered();
            OnPropertyChanged();
        }
    }

    public RelayCommand SwitchProfileCommand => new(_ => _switchProfile());

    public RelayCommand EditMyProfileCommand => new(_ =>
    {
        var vm = new ProfileEditViewModel(_profiles, ProfileEditMode.Edit, _currentProfile, allowPasswordChange: true);
        if (_showProfileEdit(vm) == true && vm.NewDbKey is { } newKey)
        {
            _db.Rekey(newKey);
            // Si ce profil était mémorisé, re-protéger la clé mise à jour.
            if (_profiles.RememberedProfileId == _currentProfile.Id)
                _profiles.RememberKey(_currentProfile.Id, newKey);
            _activityLog.Log(ActivityCategory.Reglages, "Mot de passe du profil modifié");
        }
        RaiseAll();
    });

    // ---------- Apparence ----------

    private bool _isDark;
    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (!SetProperty(ref _isDark, value)) return;
            _theme.Apply(value ? AppTheme.Dark : AppTheme.Light);
            _app.Theme = value ? AppTheme.Dark : AppTheme.Light;
            _settingsRepo.SaveApp(_app);
            StatusMessage = value ? "Thème sombre activé." : "Thème clair activé.";
            _activityLog.Log(ActivityCategory.Reglages, value ? "Thème sombre activé" : "Thème clair activé");
        }
    }

    private bool _weekStartsMonday;
    public bool WeekStartsMonday
    {
        get => _weekStartsMonday;
        set
        {
            if (!SetProperty(ref _weekStartsMonday, value)) return;
            _app.FirstDayOfWeek = value ? DayOfWeek.Monday : DayOfWeek.Sunday;
            _settingsRepo.SaveApp(_app);
            StatusMessage = "Premier jour de la semaine mis à jour.";
        }
    }

    private string _currency;
    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, value);
    }

    public RelayCommand SaveCurrencyCommand => new(_ =>
    {
        var salary = _settingsRepo.LoadSalary();
        salary.Currency = string.IsNullOrWhiteSpace(Currency) ? "€" : Currency.Trim();
        _settingsRepo.SaveSalary(salary);
        StatusMessage = "Devise enregistrée.";
        _activityLog.Log(ActivityCategory.Reglages, $"Devise changée en « {salary.Currency} »");
    });

    // ---------- Sauvegarde ----------

    public string DbPath => _db.DbPath;

    private string _lastBackupText = "Aucune sauvegarde effectuée dans cette session.";
    public string LastBackupText { get => _lastBackupText; private set => SetProperty(ref _lastBackupText, value); }

    private void BackupNow()
    {
        var folder = _dialogs.PickFolder(_app.LastBackupFolder);
        if (folder is null) return;

        try
        {
            var path = _backup.Backup(folder);
            _app.LastBackupFolder = folder;
            _settingsRepo.SaveApp(_app);
            LastBackupText = $"Dernière sauvegarde : {path}";
            StatusMessage = "Sauvegarde créée.";
            _activityLog.Log(ActivityCategory.Sauvegarde, $"Sauvegarde créée — {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Échec de la sauvegarde : " + ex.Message;
        }
    }

    private void Restore()
    {
        var file = _dialogs.OpenFile("Base Heure+ (*.db)|*.db|Tous les fichiers (*.*)|*.*", _app.LastBackupFolder);
        if (file is null) return;

        var confirm = MessageBox.Show(
            "Restaurer cette sauvegarde remplacera toutes les données actuelles.\n" +
            "L'application doit être redémarrée ensuite. Continuer ?",
            "Heure+", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _backup.Restore(file);
            _activityLog.Log(ActivityCategory.Sauvegarde, $"Sauvegarde restaurée — {Path.GetFileName(file)}");
            MessageBox.Show("Restauration effectuée. L'application va se fermer, relancez-la.",
                "Heure+", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusMessage = "Échec de la restauration : " + ex.Message;
        }
    }

    // ---------- Export ----------

    private DateTime _exportMonth;
    public DateTime ExportMonth { get => _exportMonth; set => SetProperty(ref _exportMonth, value); }

    private enum ExportKind { Pdf, Excel }

    private void Export(ExportKind kind)
    {
        int year = _exportMonth.Year, month = _exportMonth.Month;
        var salary = _settingsRepo.LoadSalary();
        var monthEntries = _entries.GetMonth(year, month);
        var stats = StatsService.Month(year, month, monthEntries, salary, _settingsRepo.LoadPrimes());

        string stamp = $"{year}-{month:00}";
        bool pdf = kind == ExportKind.Pdf;

        var path = _dialogs.SaveFile(
            pdf ? "Document PDF (*.pdf)|*.pdf" : "Classeur Excel (*.xlsx)|*.xlsx",
            $"Heure+ {stamp}.{(pdf ? "pdf" : "xlsx")}",
            _app.LastExportFolder);
        if (path is null) return;

        try
        {
            if (pdf)
                PdfExportService.ExportMonth(path, year, month, monthEntries, stats, salary);
            else
                ExcelExportService.ExportMonth(path, year, month, monthEntries, stats, salary);

            _app.LastExportFolder = Path.GetDirectoryName(path);
            _settingsRepo.SaveApp(_app);
            StatusMessage = $"Export réussi : {path}";
            _activityLog.Log(ActivityCategory.Export,
                $"Export {(pdf ? "PDF" : "Excel")} — {Fmt.Fr.TextInfo.ToTitleCase(new DateTime(year, month, 1).ToString("MMMM yyyy", Fmt.Fr))}"
                + $" · total estimé {Fmt.Money(stats.Salary.Total, salary.Currency)}");

            if (MessageBox.Show("Export terminé. Ouvrir le fichier ?", "Heure+",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                OpenPath(path);
        }
        catch (Exception ex)
        {
            StatusMessage = "Échec de l'export : " + ex.Message;
        }
    }

    // ---------- Mise à jour (GitHub Releases) ----------

    public RelayCommand CheckUpdateCommand { get; }
    public RelayCommand ApplyUpdateCommand { get; }

    /// <summary>Toujours visible : la mise à jour par GitHub marche partout.</summary>
    public bool UpdateRepoAvailable => true;

    public string UpdateModeText => _update.RunningFromSources
        ? $"Version de développement — {VersionText}"
        : $"Version installée — {VersionText}";

    private bool _updateBusy;
    private bool _updateAvailable;
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set { SetProperty(ref _updateAvailable, value); ApplyUpdateCommand.RaiseCanExecuteChanged(); }
    }

    private string _updateInfoText = "Cliquez sur « Vérifier les mises à jour ».";
    public string UpdateInfoText { get => _updateInfoText; private set => SetProperty(ref _updateInfoText, value); }

    private string _updateDetailText = "";
    public string UpdateDetailText { get => _updateDetailText; private set => SetProperty(ref _updateDetailText, value); }

    private UpdateService.ReleaseInfo? _latestRelease;

    /// <summary>Vérification silencieuse au démarrage (n'affiche rien si tout va bien / hors ligne).</summary>
    public async Task CheckUpdateOnStartupAsync()
    {
        var release = await Task.Run(() => _update.LatestReleaseAsync());
        if (release is null) return;

        var current = Assembly.GetExecutingAssembly().GetName().Version;
        if (release.Version is not null && current is not null && release.Version > current)
        {
            _latestRelease = release;
            UpdateInfoText = $"Nouvelle version disponible : {release.Tag} (vous avez la {current.ToString(3)}).";
            UpdateDetailText = release.Notes;
            UpdateAvailable = true;
        }
    }

    private async Task CheckUpdateAsync()
    {
        SetUpdateBusy(true);
        UpdateInfoText = "Vérification en cours…";
        UpdateDetailText = "";

        var release = await Task.Run(() => _update.LatestReleaseAsync());
        var current = Assembly.GetExecutingAssembly().GetName().Version;
        _latestRelease = release;

        if (release is null)
        {
            UpdateInfoText = "Impossible de contacter GitHub, ou aucune version n'a encore été publiée.";
            UpdateAvailable = false;
        }
        else if (release.Version is not null && current is not null && release.Version > current)
        {
            UpdateInfoText = $"Nouvelle version disponible : {release.Tag} (vous avez la {current.ToString(3)}).";
            UpdateDetailText = release.Notes;
            UpdateAvailable = release.InstallerUrl is not null;
            if (release.InstallerUrl is null)
                UpdateInfoText += " — aucun installeur joint à cette release.";
        }
        else
        {
            UpdateInfoText = $"L'application est à jour (version {current?.ToString(3)}).";
            UpdateDetailText = "";
            UpdateAvailable = false;
        }

        SetUpdateBusy(false);
        _activityLog.Log(ActivityCategory.Reglages, "Vérification des mises à jour — " + UpdateInfoText);
    }

    private async Task ApplyUpdateAsync()
    {
        if (_latestRelease?.InstallerUrl is not { } url)
        {
            UpdateInfoText = "Aucun installeur à télécharger. Vérifiez d'abord les mises à jour.";
            return;
        }

        string sizeText = _latestRelease.InstallerSize > 0
            ? $" (~{_latestRelease.InstallerSize / 1024 / 1024} Mo)" : "";
        if (MessageBox.Show(
                $"Télécharger et installer la version {_latestRelease.Tag}{sizeText} ?\n" +
                "L'application se fermera pour lancer l'installeur. Vos données sont conservées.",
                "Heure+", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SetUpdateBusy(true);
        UpdateInfoText = "Téléchargement de l'installeur…";

        var progress = new Progress<double>(p =>
            UpdateInfoText = $"Téléchargement de l'installeur… {p * 100:0} %");
        string? file = await _update.DownloadInstallerAsync(url, progress);

        SetUpdateBusy(false);

        if (file is null)
        {
            UpdateInfoText = "Échec du téléchargement. Réessayez ou téléchargez la release depuis GitHub.";
            return;
        }

        _activityLog.Log(ActivityCategory.Reglages, $"Installeur {_latestRelease.Tag} téléchargé, lancement…");
        UpdateService.RunInstaller(file);
        Application.Current.Shutdown();
    }

    public RelayCommand OpenReleasePageCommand => new(_ =>
    {
        if (_latestRelease is { HtmlUrl: { Length: > 0 } url })
            UpdateService.OpenReleasePage(url);
        else
            UpdateService.OpenReleasePage($"https://github.com/{UpdateService.GitHubRepo}/releases");
    });

    private void SetUpdateBusy(bool busy)
    {
        _updateBusy = busy;
        CheckUpdateCommand.RaiseCanExecuteChanged();
        ApplyUpdateCommand.RaiseCanExecuteChanged();
    }

    // ---------- À propos ----------

    public string VersionText =>
        "Version " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0");

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private static void OpenFolder(string? folder)
    {
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            OpenPath(folder);
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            /* ignore */
        }
    }
}
