using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;
using ComboBox = Avalonia.Controls.ComboBox;
using ComboBoxItem = Avalonia.Controls.ComboBoxItem;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class MirrorFeatureDialog : AWindow
{
    private ComboBox AxisComboBoxControl => this.FindControl<ComboBox>("AxisComboBox")
        ?? throw new InvalidOperationException("MirrorFeatureDialog is missing AxisComboBox.");

    public MirrorFeatureDialog()
    {
        InitializeComponent();
    }

    public MirrorFeatureDialog(string initialAxis) : this()
    {
        AxisComboBoxControl.SelectedIndex = initialAxis.ToLowerInvariant() switch
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
        ConfirmedAxis = (AxisComboBoxControl.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "x";
        Close(new MirrorFeatureDialogResult(ConfirmedAxis));
    }
}

public sealed record MirrorFeatureDialogResult(string Axis);
