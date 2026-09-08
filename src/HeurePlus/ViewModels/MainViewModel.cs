using System;
using HeurePlus.Infrastructure;
using HeurePlus.Models;

namespace HeurePlus.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Profile _profile;
    private readonly Action _switchProfile;

    public MainViewModel(
        Profile profile,
        Action switchProfile,
        CalendarViewModel calendar,
        SalaryViewModel salary,
        CalculatorViewModel calculator,
        DashboardViewModel dashboard,
        HistoryViewModel history,
        SettingsViewModel settings)
    {
        _profile = profile;
        _switchProfile = switchProfile;

        Calendar = calendar;
        Salary = salary;
        Calculator = calculator;
        Dashboard = dashboard;
        History = history;
        Settings = settings;

        SwitchProfileCommand = new RelayCommand(_ => _switchProfile());
    }

    public CalendarViewModel Calendar { get; }
    public SalaryViewModel Salary { get; }
    public CalculatorViewModel Calculator { get; }
    public DashboardViewModel Dashboard { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    public string ProfileName => _profile.Name;
    public string ProfileInitial => _profile.Initial;
    public string ProfileColorHex => _profile.ColorHex;

    public RelayCommand SwitchProfileCommand { get; }

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }
}
