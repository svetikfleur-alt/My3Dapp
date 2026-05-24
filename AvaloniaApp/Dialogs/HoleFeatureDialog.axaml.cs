using Avalonia.Interactivity;
using Avalonia.Controls;
using AWindow = Avalonia.Controls.Window;
using NumericUpDown = Avalonia.Controls.NumericUpDown;
using RadioButton = Avalonia.Controls.RadioButton;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class HoleFeatureDialog : AWindow
{
    private NumericUpDown DiameterInputControl => this.FindControl<NumericUpDown>("DiameterInput")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing DiameterInput.");

    private NumericUpDown CenterXInputControl => this.FindControl<NumericUpDown>("CenterXInput")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing CenterXInput.");

    private NumericUpDown CenterYInputControl => this.FindControl<NumericUpDown>("CenterYInput")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing CenterYInput.");

    private RadioButton ThroughAllRadioControl => this.FindControl<RadioButton>("ThroughAllRadio")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing ThroughAllRadio.");

    private RadioButton BlindRadioControl => this.FindControl<RadioButton>("BlindRadio")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing BlindRadio.");

    private NumericUpDown DepthValueInputControl => this.FindControl<NumericUpDown>("DepthValueInput")
        ?? throw new InvalidOperationException("HoleFeatureDialog is missing DepthValueInput.");

    public HoleFeatureDialog()
    {
        InitializeComponent();
    }

    public HoleFeatureDialog(double diameter, string depthKind, double depthValue, double centerOffsetX, double centerOffsetY)
        : this()
    {
        DiameterInputControl.Value = (decimal)diameter;
        CenterXInputControl.Value = (decimal)centerOffsetX;
        CenterYInputControl.Value = (decimal)centerOffsetY;

        var isBlind = string.Equals(depthKind, "Blind", StringComparison.OrdinalIgnoreCase);
        ThroughAllRadioControl.IsChecked = !isBlind;
        BlindRadioControl.IsChecked = isBlind;
        DepthValueInputControl.Value = (decimal)depthValue;
        DepthValueInputControl.IsEnabled = isBlind;
    }

    public double ConfirmedDiameter { get; private set; } = 10d;

    public string ConfirmedDepthKind { get; private set; } = "ThroughAll";

    public double ConfirmedDepthValue { get; private set; } = 20d;

    public double ConfirmedCenterOffsetX { get; private set; }

    public double ConfirmedCenterOffsetY { get; private set; }

    private void OnDepthKindChanged(object? sender, RoutedEventArgs e)
    {
        var depthValueInput = this.FindControl<NumericUpDown>("DepthValueInput");
        var blindRadio = this.FindControl<RadioButton>("BlindRadio");
        if (depthValueInput is null)
        {
            return;
        }

        depthValueInput.IsEnabled = blindRadio?.IsChecked == true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var diameter = (double)(DiameterInputControl.Value ?? 10m);
        if (diameter <= 0d)
        {
            DiameterInputControl.Value = 10m;
            return;
        }

        ConfirmedDiameter = diameter;
        ConfirmedCenterOffsetX = (double)(CenterXInputControl.Value ?? 0m);
        ConfirmedCenterOffsetY = (double)(CenterYInputControl.Value ?? 0m);

        if (BlindRadioControl.IsChecked == true)
        {
            var depthValue = (double)(DepthValueInputControl.Value ?? 20m);
            if (depthValue <= 0d)
            {
                DepthValueInputControl.Value = 20m;
                return;
            }

            ConfirmedDepthKind = "Blind";
            ConfirmedDepthValue = depthValue;
        }
        else
        {
            ConfirmedDepthKind = "ThroughAll";
            ConfirmedDepthValue = 10000d;
        }

        Close(new HoleFeatureDialogResult(ConfirmedDiameter, ConfirmedDepthKind, ConfirmedDepthValue, ConfirmedCenterOffsetX, ConfirmedCenterOffsetY));
    }
}

public sealed record HoleFeatureDialogResult(double Diameter, string DepthKind, double DepthValue, double CenterOffsetX, double CenterOffsetY);
