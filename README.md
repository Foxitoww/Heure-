# Heure+

Application **Windows (WPF / .NET 8)** de suivi d'heures pour les travailleurs intérimaires.
100 % hors ligne, données dans une base **SQLite locale**. Interface **Fluent Design / Windows 11**
(rail de navigation latéral, thème clair / sombre — **sombre par défaut**, barre de titre sombre native).

## Fonctionnalités

| Onglet | Contenu |
| --- | --- |
| **Calendrier** | Calendrier mensuel, jour sélectionnable avec **bulle animée**, panneau de détails. Ajout d'une **journée**, d'une **période**, ou d'un **cycle** (rotation *2/2*, *4/4*, *5/2*, *6/1*… avec les horaires propres à chaque jour ; option *travailler les week-ends* ; **cycle modifiable** après coup). Trait de statut coloré (travail bleu, heures sup. vert, retrait rouge, congé violet, repos jaune). Champ **heures sup. / retrait** : une valeur négative retire des heures (départ anticipé), payées au taux normal. Modes d'application : **Remplacer**, **Cumuler**, **Ignorer les jours remplis**. **Suppression multi-jours** par plage de dates. |
| **Salaire** | Deux sous-onglets : **Paramètres** (taux horaire, coeff. heures sup., IFM, congés payés, **mise à jour selon le SMIC** avec coefficient) et **Primes** (recherche dans un catalogue de ~35 primes françaises, montant ajustable, intégrées à l'estimation et aux exports). Estimation détaillée (ce mois, mois précédent, tout l'historique). |
| **Calculatrice** | Calculatrice classique (souris + clavier) et **conversions heures / minutes**. |
| **Tableau de bord** | Totaux du mois + mini-graphiques par jour / semaine + répartition des jours. |
| **Historique** | Journal horodaté des actions (saisies, périodes, cycles, suppressions, réglages, primes, sauvegardes, exports). |
| **Réglages** | Thème clair / sombre, premier jour de la semaine, devise, **sauvegarde / restauration** SQLite, **export PDF et Excel**, **mise à jour de l'application via git** (branche `0.1` : « Vérifier les mises à jour » + fast-forward). |

Les exports **PDF** et **Excel** mettent en avant le **total à payer (estimé)** et détaillent le montant **par jour**.

> L'onglet Salaire et les exports fournissent une **estimation indicative** ; ils ne constituent pas un bulletin de paie.

## Compiler et lancer (développement)

Prérequis : **SDK .NET 8+** (`winget install --id Microsoft.DotNet.SDK.8 -e`, puis rouvrir un terminal).

```bash
dotnet run --project src/HeurePlus/HeurePlus.csproj
```

La base est créée au premier lancement dans `%LOCALAPPDATA%\HeurePlus\heureplus.db`.

## Exécutable autonome (portable, sans installation)

```bash
dotnet publish src/HeurePlus/HeurePlus.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Résultat : `src/HeurePlus/bin/Release/net8.0-windows/win-x64/publish/`
→ **`HeurePlus.exe`** (~78 Mo, aucun runtime .NET requis) + le dossier `LatoFont/` (à garder à côté de l'exe).

## Installeur `HeurePlus-Setup.exe`

Prérequis en plus : **Inno Setup 6** (`winget install --id JRSoftware.InnoSetup -e`).

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Publie l'exe autonome puis compile → **`installer/Output/HeurePlus-Setup.exe`**.
Installation **par utilisateur** (pas d'admin), assistant en français, raccourcis menu Démarrer
(+ bureau en option), désinstalleur. La base de données de l'utilisateur (`%LOCALAPPDATA%\HeurePlus`)
n'est **pas** supprimée à la désinstallation.

## Architecture

MVVM sans framework externe (`Infrastructure/ObservableObject`, `RelayCommand`, `AppEvents`).

```
src/HeurePlus/
  Models/        DayEntry, DayStatus, SalarySettings, AppSettings, MonthStats, ActivityEntry
  Data/          AppDatabase (SQLite), DayEntryRepository, SettingsRepository, ActivityLogRepository
  Services/      SalaryCalculator, StatsService, ThemeManager, WindowEffects (DWM),
                 BackupService, PdfExportService, ExcelExportService, DialogService
  ViewModels/    Main, Calendar (+ DayCell / DayDetails), EntryEditor (+ CycleStep),
                 Salary, Calculator, Dashboard, History, Settings
  Views/         MainWindow (rail de nav) + 6 vues d'onglet + EntryEditorWindow
  Themes/        Shared.xaml (composants Fluent), Light.xaml, Dark.xaml
  Converters/
installer/       HeurePlus.iss (Inno Setup) + build-installer.ps1
```

## Dépendances NuGet

- `Microsoft.Data.Sqlite` — base locale (moteur SQLite natif embarqué)
- `ClosedXML` — export `.xlsx`
- `QuestPDF` — export `.pdf` (licence Community, usage individuel / petite structure)
