using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class RenameDialog : AWindow
{
    public RenameDialog()
    {
        InitializeComponent();
    }

    public RenameDialog(string currentName) : this()
    {
        NameInput.Text = currentName;
        NameInput.SelectAll();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var name = NameInput.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            Close(name);
        }
    }

    private void OnInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnConfirmClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }
}
