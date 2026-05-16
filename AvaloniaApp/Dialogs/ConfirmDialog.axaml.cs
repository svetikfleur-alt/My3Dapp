using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class ConfirmDialog : AWindow
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        Title = title;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
