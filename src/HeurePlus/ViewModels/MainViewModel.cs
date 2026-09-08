using HeurePlus.Infrastructure;

namespace HeurePlus.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public MainViewModel(
        CalendarViewModel calendar,
        SalaryViewModel salary,
        CalculatorViewModel calculator,
        DashboardViewModel dashboard,
        SettingsViewModel settings)
    {
        Calendar = calendar;
        Salary = salary;
        Calculator = calculator;
        Dashboard = dashboard;
        Settings = settings;
    }

    public CalendarViewModel Calendar { get; }
    public SalaryViewModel Salary { get; }
    public CalculatorViewModel Calculator { get; }
    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }
}
