using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class CircularPatternDialog : AWindow
{
    public CircularPatternDialog()
    {
        InitializeComponent();
    }

    public CircularPatternDialog(int initialCount, double initialAngle, string initialAxis)
        : this()
    {
        CountInput.Value = initialCount;
        AngleInput.Value = (decimal)initialAngle;
        AxisComboBox.SelectedIndex = initialAxis.Trim().ToLowerInvariant() switch
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
        var count = (int)(CountInput.Value ?? 4m);
        if (count < 2)
        {
            CountInput.Value = 2m;
            return;
        }

        var angle = (double)(AngleInput.Value ?? 360m);
        if (angle <= 0d)
        {
            AngleInput.Value = 1m;
            return;
        }

        ConfirmedCount = count;
        ConfirmedAngle = angle;
        ConfirmedAxis = (AxisComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? "y";
        Close(new CircularPatternDialogResult(ConfirmedCount, ConfirmedAngle, ConfirmedAxis));
    }
}

public sealed record CircularPatternDialogResult(int Count, double TotalAngle, string Axis);
