using Avalonia.Controls;
using Avalonia.Input;
using TileGen.App.ViewModels;

namespace TileGen.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Dialogs = new Dialogs(this);
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Space rerolls the seed unless a text field has the focus, because a space in a name is still a space.
        if (!e.Handled && e.Key == Key.Space && DataContext is MainViewModel vm
            && FocusManager?.GetFocusedElement() is not (TextBox or NumericUpDown))
        {
            vm.RerollSeedCommand.Execute(null);
            e.Handled = true;
        }
    }
}
