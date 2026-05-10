using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class LinearPatternDialog : AWindow
{
    public LinearPatternDialog()
    {
        InitializeComponent();
    }

    public LinearPatternDialog(int initialCount, double initialSpacing, string initialAxis)
        : this()
    {
        CountInput.Value = initialCount;
        SpacingInput.Value = (decimal)initialSpacing;
        AxisComboBox.SelectedIndex = initialAxis.Trim().ToLowerInvariant() switch
        {
            "y" => 1,
            "z" => 2,
            _ => 0
        };
    }

    public int ConfirmedCount { get; private set; } = 3;

    public double ConfirmedSpacing { get; private set; } = 20d;

    public string ConfirmedAxis { get; private set; } = "x";

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        ConfirmedCount = Math.Max(2, (int)(CountInput.Value ?? 3m));
        ConfirmedSpacing = Math.Max(0.1d, (double)(SpacingInput.Value ?? 20m));
        ConfirmedAxis = (AxisComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? "x";
        Close(new LinearPatternDialogResult(ConfirmedCount, ConfirmedSpacing, ConfirmedAxis));
    }
}

public sealed record LinearPatternDialogResult(int Count, double Spacing, string Axis);
