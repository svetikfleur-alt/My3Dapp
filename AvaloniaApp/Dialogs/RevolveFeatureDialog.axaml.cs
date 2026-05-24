using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;
using CheckBox = Avalonia.Controls.CheckBox;
using ComboBox = Avalonia.Controls.ComboBox;
using ComboBoxItem = Avalonia.Controls.ComboBoxItem;
using Control = Avalonia.Controls.Control;
using NumericUpDown = Avalonia.Controls.NumericUpDown;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class RevolveFeatureDialog : AWindow
{
    private TextBlock ProfileValueTextBlockControl => this.FindControl<TextBlock>("ProfileValueTextBlock")
        ?? throw new InvalidOperationException("RevolveFeatureDialog is missing ProfileValueTextBlock.");

    private NumericUpDown AngleInputControl => this.FindControl<NumericUpDown>("AngleInput")
        ?? throw new InvalidOperationException("RevolveFeatureDialog is missing AngleInput.");

    private ComboBox AxisComboBoxControl => this.FindControl<ComboBox>("AxisComboBox")
        ?? throw new InvalidOperationException("RevolveFeatureDialog is missing AxisComboBox.");

    private CheckBox FullRevolveCheckBoxControl => this.FindControl<CheckBox>("FullRevolveCheckBox")
        ?? throw new InvalidOperationException("RevolveFeatureDialog is missing FullRevolveCheckBox.");

    private Control AngleRowControl => this.FindControl<Control>("AngleRow")
        ?? throw new InvalidOperationException("RevolveFeatureDialog is missing AngleRow.");

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
        ProfileValueTextBlockControl.Text = string.IsNullOrWhiteSpace(profileSummary)
            ? "Select a closed sketch profile"
            : profileSummary;
        AngleInputControl.Value = (decimal)(initialAngle >= 360 ? 360 : initialAngle);
        AxisComboBoxControl.SelectedIndex = string.Equals(initialAxis, "x", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

        var isFullRevolve = initialAngle >= 360;
        FullRevolveCheckBoxControl.IsChecked = isFullRevolve;
        AngleRowControl.IsVisible = !isFullRevolve;
    }

    public double ConfirmedAngle { get; private set; }
    public string ConfirmedAxis { get; private set; } = "y";

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var fullRevolve = FullRevolveCheckBoxControl.IsChecked == true;
        ConfirmedAngle = fullRevolve ? 360.0 : (double)(AngleInputControl.Value ?? 360m);
        var selectedAxis = (AxisComboBoxControl.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        ConfirmedAxis = string.Equals(selectedAxis, "x", StringComparison.OrdinalIgnoreCase) ? "x" : "y";
        Close(new RevolveFeatureDialogResult(ConfirmedAngle, ConfirmedAxis));
    }

    private void OnFullRevolveChanged(object? sender, RoutedEventArgs e)
    {
        AngleRowControl.IsVisible = FullRevolveCheckBoxControl.IsChecked != true;
    }
}

public sealed record RevolveFeatureDialogResult(double Angle, string Axis);
