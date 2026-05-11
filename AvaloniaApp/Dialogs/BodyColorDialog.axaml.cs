using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AButton = Avalonia.Controls.Button;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Simple color picker showing preset swatches.
/// Returns: null = cancel, "" = reset to default, "#rrggbb" = color.
/// </summary>
public sealed partial class BodyColorDialog : AWindow
{
    public BodyColorDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AButton { Tag: string color })
            Close(color);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => Close(string.Empty);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
