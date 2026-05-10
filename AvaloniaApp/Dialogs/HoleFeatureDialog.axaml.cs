using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class HoleFeatureDialog : AWindow
{
    public HoleFeatureDialog()
    {
        InitializeComponent();
    }

    public HoleFeatureDialog(double diameter, string depthKind, double depthValue, double centerOffsetX, double centerOffsetY)
        : this()
    {
        DiameterInput.Value = (decimal)diameter;
        CenterXInput.Value = (decimal)centerOffsetX;
        CenterYInput.Value = (decimal)centerOffsetY;

        var isBlind = string.Equals(depthKind, "Blind", StringComparison.OrdinalIgnoreCase);
        ThroughAllRadio.IsChecked = !isBlind;
        BlindRadio.IsChecked = isBlind;
        DepthValueInput.Value = (decimal)depthValue;
        DepthValueInput.IsEnabled = isBlind;
    }

    public double ConfirmedDiameter { get; private set; } = 10d;

    public string ConfirmedDepthKind { get; private set; } = "ThroughAll";

    public double ConfirmedDepthValue { get; private set; } = 20d;

    public double ConfirmedCenterOffsetX { get; private set; }

    public double ConfirmedCenterOffsetY { get; private set; }

    private void OnDepthKindChanged(object? sender, RoutedEventArgs e)
    {
        if (DepthValueInput is null)
        {
            return;
        }

        DepthValueInput.IsEnabled = BlindRadio?.IsChecked == true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var diameter = (double)(DiameterInput.Value ?? 10m);
        if (diameter <= 0d)
        {
            DiameterInput.Value = 10m;
            return;
        }

        ConfirmedDiameter = diameter;
        ConfirmedCenterOffsetX = (double)(CenterXInput.Value ?? 0m);
        ConfirmedCenterOffsetY = (double)(CenterYInput.Value ?? 0m);

        if (BlindRadio.IsChecked == true)
        {
            var depthValue = (double)(DepthValueInput.Value ?? 20m);
            if (depthValue <= 0d)
            {
                DepthValueInput.Value = 20m;
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
