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
        var profile = ResolveProfile();
        if (profile is null) { Shutdown(); return; }

        _profiles.LastActiveId = profile.Id;
        profile.LastUsedUtc = DateTime.UtcNow;
        _profiles.Update(profile);

        ShowMainFor(profile);
    }

    private Profile? ResolveProfile()
    {
        var remembered = _profiles.Get(_profiles.RememberedProfileId);
        if (remembered is { HasCredentials: true }) return remembered;

        Func<LoginViewModel, bool?> showLogin = vm => ShowModal(new LoginWindow(), vm);
        Func<ProfileEditViewModel, bool?> showEdit = vm => ShowModal(new ProfileEditWindow(), vm);
        var picker = new ProfilePickerViewModel(_profiles, showLogin, showEdit);
        ShowModal(new ProfilePickerWindow(), picker);
        return picker.Result;
    }

    private bool? ShowModal(Window w, object dataContext)
    {
        w.DataContext = dataContext;
        if (_mainWindow != null) w.Owner = _mainWindow;
        WindowEffects.Apply(w, _theme.Current == Models.AppTheme.Dark);
        return w.ShowDialog();
    }

    private void ShowMainFor(Profile profile)
    {
        // --- Composition (pas de conteneur DI, câblage manuel) ---
        var database = new AppDatabase(_profiles.DbPathFor(profile.Id));

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
            new SettingsViewModel(settingsRepo, backup, _theme, dialogs, entryRepo, activityLog, events, database, _profiles, profile, SwitchProfile, showProfileEdit));

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
        _profiles.RememberedProfileId = null;

        _switching = true;
        var old = _mainWindow;
        _mainWindow = null;
        old?.Close();
        _switching = false;
        StartProfileFlow();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Une erreur inattendue s'est produite :\n\n" + e.Exception.Message,
            "Heure+", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
