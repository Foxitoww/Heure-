using System.Windows;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class ProfileEditWindow : Window
{
    public ProfileEditWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is ProfileEditViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is ProfileEditViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result) => DialogResult = result;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileEditViewModel vm)
            vm.TrySave(Pw.Password, Pw2.Password);
    }
}
