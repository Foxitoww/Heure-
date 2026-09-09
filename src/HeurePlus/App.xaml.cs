using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Models;
using HeurePlus.Services;
using HeurePlus.ViewModels;
using HeurePlus.Views;

namespace HeurePlus;

public partial class App : Application
{
    private readonly ThemeManager _theme = new();
    private ProfileStore _profiles = null!;
    private string _dataDir = "";
    private Window? _mainWindow;
    private bool _switching;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Moteur SQLite chiffrant (SQLCipher) : enregistre le fournisseur natif.
        SQLitePCL.Batteries_V2.Init();

        // Licence QuestPDF (usage individuel / petite structure).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        DispatcherUnhandledException += OnUnhandledException;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeurePlus");
        _profiles = new ProfileStore(_dataDir);
        _profiles.EnsureMigratedLegacy();
        _theme.Apply(Models.AppTheme.Dark); // thème de secours pour les écrans de connexion

        StartProfileFlow();
    }

    private void StartProfileFlow()
    {
        var session = ResolveProfile();
        if (session is null || session.Value.Profile is null) { Shutdown(); return; }

        var profile = session.Value.Profile;
        var key = session.Value.Key;

        _profiles.LastActiveId = profile.Id;
        profile.LastUsedUtc = DateTime.UtcNow;
        _profiles.Update(profile);

        ShowMainFor(profile, key);
    }

    /// <summary>Résout le profil à ouvrir et sa clé de base (mémorisé, ou via l'écran de sélection).</summary>
    private (Profile? Profile, byte[] Key)? ResolveProfile()
    {
        if (_profiles.Get(_profiles.RememberedProfileId) is { HasCredentials: true } remembered
            && _profiles.TryGetRememberedKey() is { } rememberedKey)
        {
            return (remembered, rememberedKey);
        }

        Func<LoginViewModel, bool?> showLogin = vm => ShowModal(new LoginWindow(), vm);
        Func<ProfileEditViewModel, bool?> showEdit = vm => ShowModal(new ProfileEditWindow(), vm);
        var picker = new ProfilePickerViewModel(_profiles, showLogin, showEdit);
        ShowModal(new ProfilePickerWindow(), picker);

        if (picker.Result is null || picker.ResultKey is null) return null;

        // Profil créé avant le chiffrement : on fige le sel utilisé pour la clé.
        if (picker.Result.DbKdfSalt is null && picker.ResultKdfSalt is not null)
        {
            picker.Result.DbKdfSalt = Convert.ToBase64String(picker.ResultKdfSalt);
            _profiles.Update(picker.Result);
        }

        return (picker.Result, picker.ResultKey);
    }

    private bool? ShowModal(Window w, object dataContext)
    {
        w.DataContext = dataContext;
        if (_mainWindow != null) w.Owner = _mainWindow;
        WindowEffects.Apply(w, _theme.Current == Models.AppTheme.Dark);
        return w.ShowDialog();
    }

    private void ShowMainFor(Profile profile, byte[] key)
    {
        // --- Composition (pas de conteneur DI, câblage manuel) ---
        string dbPath = _profiles.DbPathFor(profile.Id);

        // Base héritée en clair (profil « Moi » de la 0.2.0 qui vient de se voir
        // attribuer un mot de passe) → on la chiffre avant la première ouverture.
        if (ProfileDbSecurity.IsPlaintextDatabase(dbPath))
            ProfileDbSecurity.EncryptInPlace(dbPath, key);

        var database = new AppDatabase(dbPath, key);

        // Le chiffrement est confirmé fonctionnel : on efface la copie en clair
        // laissée par la migration multi-profils (choix « supprimer »).
        TryDeletePlaintextBackups();

        var events = new AppEvents();
        var entryRepo = new DayEntryRepository(database, events);
        var settingsRepo = new SettingsRepository(database, events);
        var activityLog = new ActivityLogRepository(database, events);
        var cycleRepo = new CycleRepository(database);
        var dialogs = new DialogService();
        var backup = new BackupService(database);

        _theme.Apply(settingsRepo.LoadApp().Theme);

        Func<EntryEditorViewModel, bool?> showEditor = vm =>
        {
            var window = new EntryEditorWindow
            {
                DataContext = vm,
                Owner = Current.MainWindow
            };
            WindowEffects.Apply(window, _theme.Current == Models.AppTheme.Dark);
            return window.ShowDialog();
        };

        Func<RangeDeleteViewModel, bool?> showRangeDelete = vm =>
        {
            var window = new RangeDeleteWindow
            {
                DataContext = vm,
                Owner = Current.MainWindow
            };
            WindowEffects.Apply(window, _theme.Current == Models.AppTheme.Dark);
            return window.ShowDialog();
        };

        Func<CyclesViewModel, bool?> showCyclesManager = vm =>
        {
            var window = new CyclesWindow
            {
                DataContext = vm,
                Owner = Current.MainWindow
            };
            WindowEffects.Apply(window, _theme.Current == Models.AppTheme.Dark);
            return window.ShowDialog();
        };

        Func<ProfileEditViewModel, bool?> showProfileEdit = vm => ShowModal(new ProfileEditWindow(), vm);

        var main = new MainViewModel(
            profile,
            SwitchProfile,
            new CalendarViewModel(entryRepo, settingsRepo, activityLog, cycleRepo, events, showEditor, showRangeDelete, showCyclesManager),
            new SalaryViewModel(entryRepo, settingsRepo, activityLog, events),
            new CalculatorViewModel(),
            new DashboardViewModel(entryRepo, settingsRepo, events),
            new HistoryViewModel(activityLog, events),
            new SettingsViewModel(settingsRepo, backup, _theme, dialogs, entryRepo, activityLog, events, database, _profiles, profile, key, SwitchProfile, showProfileEdit));

        var mainWindow = new MainWindow { DataContext = main };
        MainWindow = mainWindow;
        _mainWindow = mainWindow;
        mainWindow.Closed += (_, _) => { if (!_switching) Shutdown(); };
        mainWindow.Show();

        // Effets Windows 11 (barre de titre sombre, coins arrondis) + suivi du thème.
        WindowEffects.Apply(mainWindow, _theme.Current == Models.AppTheme.Dark);
        events.SettingsChanged += () => mainWindow.Dispatcher.Invoke(
            () => WindowEffects.Apply(mainWindow, _theme.Current == Models.AppTheme.Dark));
    }

    private void SwitchProfile()
    {
        // « Changer de profil » doit toujours ramener à l'écran de sélection,
        // même si « rester connecté » était coché pour le profil courant.
        _profiles.ForgetRemembered();

        _switching = true;
        var old = _mainWindow;
        _mainWindow = null;
        old?.Close();
        _switching = false;
        StartProfileFlow();
    }

    /// <summary>Efface les copies de base laissées en clair sur le disque (post-chiffrement).</summary>
    private void TryDeletePlaintextBackups()
    {
        try
        {
            string legacyBak = Path.Combine(_dataDir, "heureplus.db.premultiprofile.bak");
            if (File.Exists(legacyBak)) File.Delete(legacyBak);
        }
        catch
        {
            // Fichier verrouillé ou déjà absent : sans conséquence.
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Une erreur inattendue s'est produite :\n\n" + e.Exception.Message,
            "Heure+", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
