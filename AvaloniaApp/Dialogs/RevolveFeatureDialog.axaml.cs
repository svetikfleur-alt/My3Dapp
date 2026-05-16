using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class RevolveFeatureDialog : AWindow
{
    public RevolveFeatureDialog()
    {
        InitializeComponent();
    }

    public RevolveFeatureDialog(
        string profileSummary,
        string planeSummary,
        double initialAngle,
        string initialAxis)
        : this()
    {
        ProfileValueTextBlock.Text = string.IsNullOrWhiteSpace(profileSummary)
            ? "Select a closed sketch profile"
            : profileSummary;
        AngleInput.Value = (decimal)(initialAngle >= 360 ? 360 : initialAngle);
        AxisComboBox.SelectedIndex = string.Equals(initialAxis, "x", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

        var isFullRevolve = initialAngle >= 360;
        FullRevolveCheckBox.IsChecked = isFullRevolve;
        AngleRow.IsVisible = !isFullRevolve;
    }

    public double ConfirmedAngle { get; private set; }
    public string ConfirmedAxis { get; private set; } = "y";

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var fullRevolve = FullRevolveCheckBox.IsChecked == true;
        ConfirmedAngle = fullRevolve ? 360.0 : (double)(AngleInput.Value ?? 360m);
        var selectedAxis = (AxisComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        ConfirmedAxis = string.Equals(selectedAxis, "x", StringComparison.OrdinalIgnoreCase) ? "x" : "y";
        Close(new RevolveFeatureDialogResult(ConfirmedAngle, ConfirmedAxis));
    }

    private void OnFullRevolveChanged(object? sender, RoutedEventArgs e)
    {
        AngleRow.IsVisible = FullRevolveCheckBox.IsChecked != true;
    }
}

public sealed record RevolveFeatureDialogResult(double Angle, string Axis);
