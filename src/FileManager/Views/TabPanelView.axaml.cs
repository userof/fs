using Avalonia.Controls;
using Avalonia.Input;
using FileManager.ViewModels;

namespace FileManager.Views;

public partial class TabPanelView : UserControl
{
    public TabPanelView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TabPanelViewModel vm) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (ctrl && e.Key == Key.T)
        {
            vm.AddTabCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.W)
        {
            vm.CloseTabCommand.Execute(vm.SelectedTab);
            e.Handled = true;
        }
    }
}
