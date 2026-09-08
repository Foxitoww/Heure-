using System.Windows.Controls;
using System.Windows.Input;
using HeurePlus.ViewModels;

namespace HeurePlus.Views;

public partial class CalculatorView : UserControl
{
    public CalculatorView()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CalculatorViewModel vm) return;

        string? token = e.Key switch
        {
            >= Key.D0 and <= Key.D9 when e.KeyboardDevice.Modifiers == ModifierKeys.None
                => ((int)(e.Key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9
                => ((int)(e.Key - Key.NumPad0)).ToString(),
            Key.Add or Key.OemPlus => "+",
            Key.Subtract or Key.OemMinus => "-",
            Key.Multiply => "×",
            Key.Divide => "÷",
            Key.Decimal or Key.OemComma or Key.OemPeriod => ",",
            Key.Enter => "=",
            Key.Back => "back",
            Key.Escape => "C",
            Key.Delete => "CE",
            _ => null
        };

        if (token is null) return;
        vm.Key(token);
        e.Handled = true;
    }
}
