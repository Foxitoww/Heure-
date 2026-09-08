using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HeurePlus.Data;
using HeurePlus.Infrastructure;
using HeurePlus.Services;
using HeurePlus.ViewModels;
using HeurePlus.Views;

namespace HeurePlus;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Licence QuestPDF (usage individuel / petite structure).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        DispatcherUnhandledException += OnUnhandledException;

        // --- Composition (pas de conteneur DI, câblage manuel) ---
        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeurePlus");
        var database = new AppDatabase(Path.Combine(dataDir, "heureplus.db"));

        var events = new AppEvents();
        var entryRepo = new DayEntryRepository(database, events);
        var settingsRepo = new SettingsRepository(database, events);
        var dialogs = new DialogService();
        var backup = new BackupService(database);
        var theme = new ThemeManager();

        theme.Apply(settingsRepo.LoadApp().Theme);

        Func<EntryEditorViewModel, bool?> showEditor = vm =>
        {
            var window = new EntryEditorWindow
            {
                DataContext = vm,
                Owner = Current.MainWindow
            };
            WindowEffects.Apply(window, theme.Current == Models.AppTheme.Dark);
            return window.ShowDialog();
        };

        var main = new MainViewModel(
            new CalendarViewModel(entryRepo, settingsRepo, events, showEditor),
            new SalaryViewModel(entryRepo, settingsRepo, events),
            new CalculatorViewModel(),
            new DashboardViewModel(entryRepo, settingsRepo, events),
            new SettingsViewModel(settingsRepo, backup, theme, dialogs, entryRepo, events, database));

        var mainWindow = new MainWindow { DataContext = main };
        MainWindow = mainWindow;
        mainWindow.Show();

        // Effets Windows 11 (barre de titre sombre, coins arrondis) + suivi du thème.
        WindowEffects.Apply(mainWindow, theme.Current == Models.AppTheme.Dark);
        events.SettingsChanged += () => mainWindow.Dispatcher.Invoke(
            () => WindowEffects.Apply(mainWindow, theme.Current == Models.AppTheme.Dark));
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Une erreur inattendue s'est produite :\n\n" + e.Exception.Message,
            "Heure+", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
