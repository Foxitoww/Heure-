using System.Windows;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class RangeDeleteWindow : Window
{
    public RangeDeleteWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is RangeDeleteViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is RangeDeleteViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result) => DialogResult = result;
}
