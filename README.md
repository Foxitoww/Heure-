# Heure+

Application **Windows (WPF / .NET 8)** de calendrier pour les travailleurs intérimaires.
100 % hors ligne, données stockées dans une base **SQLite locale**.

## Fonctionnalités

| Onglet | Contenu |
| --- | --- |
| **Calendrier** | Calendrier mensuel, chaque jour sélectionnable avec une **bulle animée**. Panneau de détails en dessous pour consulter / modifier. Ajout d'une **journée** ou d'une **période** (statut *Travail*, *Heures sup.*, *Congé*, *Repos* ; horaires, pause, heures normales / sup., taux spécifique, note). |
| **Salaire** | Taux horaire, coefficient de majoration des heures sup., options **IFM** (prime de précarité) et **congés payés**. Estimation détaillée (mois en cours, mois précédent ou tout l'historique). |
| **Calculatrice** | Calculatrice classique (souris + clavier) et **conversions heures / minutes** (décimal ⇄ h:mm, addition / soustraction de durées). |
| **Tableau de bord** | Totaux du mois (heures, heures sup., congés, jours travaillés, salaire estimé) + mini-graphiques par jour et par semaine + répartition des jours. |
| **Réglages** | Thème **clair / sombre**, premier jour de la semaine, devise, **sauvegarde / restauration** de la base, **export PDF et Excel**. |

## Prérequis

Cette machine a les *runtimes* .NET mais **pas le SDK**. Pour compiler / lancer, installer le **SDK .NET 8** :

```bash
winget install --id Microsoft.DotNet.SDK.8 -e
```

(ou télécharger depuis <https://dotnet.microsoft.com/download/dotnet/8.0>)

Ouvrir un **nouveau** terminal après l'installation pour que `dotnet` soit dans le `PATH`.

## Compiler et lancer

```bash
dotnet restore HeurePlus.sln
dotnet run --project src/HeurePlus/HeurePlus.csproj
```

Première exécution : la base est créée dans
`%LOCALAPPDATA%\HeurePlus\heureplus.db`.

Générer un exécutable autonome :

```bash
dotnet publish src/HeurePlus/HeurePlus.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Dépendances NuGet

- `Microsoft.Data.Sqlite` — base locale (moteur SQLite natif embarqué)
- `ClosedXML` — export `.xlsx`
- `QuestPDF` — export `.pdf` (licence Community, usage individuel / petite structure)

Les numéros de version dans `HeurePlus.csproj` peuvent être ajustés si le restore échoue.

## Architecture

MVVM sans framework externe (`Infrastructure/ObservableObject`, `RelayCommand`, `AppEvents`).

```
src/HeurePlus/
  Models/        DayEntry, DayStatus, SalarySettings, AppSettings, MonthStats
  Data/          AppDatabase (SQLite), DayEntryRepository, SettingsRepository
  Services/      SalaryCalculator, StatsService, ThemeManager,
                 BackupService, PdfExportService, ExcelExportService, DialogService
  ViewModels/    Main, Calendar (+ DayCell / DayDetails), EntryEditor,
                 Salary, Calculator, Dashboard, Settings
  Views/         MainWindow + 5 vues d'onglet + EntryEditorWindow
  Themes/        Shared.xaml (styles + animation de la bulle), Light.xaml, Dark.xaml
  Converters/
```

> L'onglet Salaire et les exports fournissent une **estimation indicative** : ils ne
> constituent pas un bulletin de paie.
