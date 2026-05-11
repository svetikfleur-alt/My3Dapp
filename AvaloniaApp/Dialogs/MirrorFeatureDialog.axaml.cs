using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class MirrorFeatureDialog : AWindow
{
    public MirrorFeatureDialog()
    {
        InitializeComponent();
    }

    public MirrorFeatureDialog(string initialAxis) : this()
    {
        AxisComboBox.SelectedIndex = initialAxis.ToLowerInvariant() switch
        {
            "y" => 1,
            "z" => 2,
            _ => 0
        };
    }

    public string ConfirmedAxis { get; private set; } = "x";

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        ConfirmedAxis = (AxisComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "x";
        Close(new MirrorFeatureDialogResult(ConfirmedAxis));
    }
}

public sealed record MirrorFeatureDialogResult(string Axis);
