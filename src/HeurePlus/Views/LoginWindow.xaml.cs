using System.Windows;
using System.Windows.Input;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is LoginViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is LoginViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result) => DialogResult = result;

    private void OnConnect(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.TryLogin(Pw.Password);
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnConnect(sender, e);
    }
}
