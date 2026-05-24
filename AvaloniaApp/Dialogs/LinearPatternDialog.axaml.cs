using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;
using ComboBox = Avalonia.Controls.ComboBox;
using ComboBoxItem = Avalonia.Controls.ComboBoxItem;
using NumericUpDown = Avalonia.Controls.NumericUpDown;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class LinearPatternDialog : AWindow
{
    private NumericUpDown CountInputControl => this.FindControl<NumericUpDown>("CountInput")
        ?? throw new InvalidOperationException("LinearPatternDialog is missing CountInput.");

    private NumericUpDown SpacingInputControl => this.FindControl<NumericUpDown>("SpacingInput")
        ?? throw new InvalidOperationException("LinearPatternDialog is missing SpacingInput.");

    private ComboBox AxisComboBoxControl => this.FindControl<ComboBox>("AxisComboBox")
        ?? throw new InvalidOperationException("LinearPatternDialog is missing AxisComboBox.");

    public LinearPatternDialog()
    {
        InitializeComponent();
    }

    public LinearPatternDialog(int initialCount, double initialSpacing, string initialAxis)
        : this()
    {
        CountInputControl.Value = initialCount;
        SpacingInputControl.Value = (decimal)initialSpacing;
        AxisComboBoxControl.SelectedIndex = initialAxis.Trim().ToLowerInvariant() switch
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
        ConfirmedCount = Math.Max(2, (int)(CountInputControl.Value ?? 3m));
        ConfirmedSpacing = Math.Max(0.1d, (double)(SpacingInputControl.Value ?? 20m));
        ConfirmedAxis = (AxisComboBoxControl.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? "x";
        Close(new LinearPatternDialogResult(ConfirmedCount, ConfirmedSpacing, ConfirmedAxis));
    }
}

public sealed record LinearPatternDialogResult(int Count, double Spacing, string Axis);
