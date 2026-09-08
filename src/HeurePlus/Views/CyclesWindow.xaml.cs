using System.Windows;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class CyclesWindow : Window
{
    public CyclesWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is CyclesViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is CyclesViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result) => DialogResult = result;
}
