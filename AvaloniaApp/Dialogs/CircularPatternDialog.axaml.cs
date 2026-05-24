using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;
using ComboBox = Avalonia.Controls.ComboBox;
using ComboBoxItem = Avalonia.Controls.ComboBoxItem;
using NumericUpDown = Avalonia.Controls.NumericUpDown;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class CircularPatternDialog : AWindow
{
    private NumericUpDown CountInputControl => this.FindControl<NumericUpDown>("CountInput")
        ?? throw new InvalidOperationException("CircularPatternDialog is missing CountInput.");

    private NumericUpDown AngleInputControl => this.FindControl<NumericUpDown>("AngleInput")
        ?? throw new InvalidOperationException("CircularPatternDialog is missing AngleInput.");

    private ComboBox AxisComboBoxControl => this.FindControl<ComboBox>("AxisComboBox")
        ?? throw new InvalidOperationException("CircularPatternDialog is missing AxisComboBox.");

    public CircularPatternDialog()
    {
        InitializeComponent();
    }

    public CircularPatternDialog(int initialCount, double initialAngle, string initialAxis)
        : this()
    {
        CountInputControl.Value = initialCount;
        AngleInputControl.Value = (decimal)initialAngle;
        AxisComboBoxControl.SelectedIndex = initialAxis.Trim().ToLowerInvariant() switch
        {
            "x" => 0,
            "z" => 2,
            _ => 1
        };
    }

    public int ConfirmedCount { get; private set; } = 4;

    public double ConfirmedAngle { get; private set; } = 360d;

    public string ConfirmedAxis { get; private set; } = "y";

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var count = (int)(CountInputControl.Value ?? 4m);
        if (count < 2)
        {
            CountInputControl.Value = 2m;
            return;
        }

        var angle = (double)(AngleInputControl.Value ?? 360m);
        if (angle <= 0d)
        {
            AngleInputControl.Value = 1m;
            return;
        }

        ConfirmedCount = count;
        ConfirmedAngle = angle;
        ConfirmedAxis = (AxisComboBoxControl.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? "y";
        Close(new CircularPatternDialogResult(ConfirmedCount, ConfirmedAngle, ConfirmedAxis));
    }
}

public sealed record CircularPatternDialogResult(int Count, double TotalAngle, string Axis);
