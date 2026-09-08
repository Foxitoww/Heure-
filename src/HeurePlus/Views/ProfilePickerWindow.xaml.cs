using System;
using System.Windows;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class ProfilePickerWindow : Window
{
    public ProfilePickerWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is ProfilePickerViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is ProfilePickerViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result)
    {
        try
        {
            DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            // Fenêtre affichée hors mode modal : DialogResult n'est pas disponible.
            Close();
        }
    }
}
