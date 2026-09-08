using System.Windows;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class EntryEditorWindow : Window
{
    public EntryEditorWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is EntryEditorViewModel oldVm)
                oldVm.CloseRequested -= OnCloseRequested;
            if (args.NewValue is EntryEditorViewModel newVm)
                newVm.CloseRequested += OnCloseRequested;
        };
    }

    private void OnCloseRequested(bool result)
    {
        // Sur une fenêtre modale, définir DialogResult referme la fenêtre.
        DialogResult = result;
    }
}
