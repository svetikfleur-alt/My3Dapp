using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class SweepFeatureDialog : AWindow
{
    public SweepFeatureDialog()
    {
        InitializeComponent();
    }

    public SweepFeatureDialog(
        string profileSummary,
        string planeSummary,
        double initialDistance,
        double initialTwist)
        : this()
    {
        ProfileValueTextBlock.Text = string.IsNullOrWhiteSpace(profileSummary)
            ? "Select a closed sketch profile"
            : profileSummary;
        DistanceInput.Value = (decimal)initialDistance;
        TwistInput.Value = (decimal)initialTwist;
    }

    public double ConfirmedDistance { get; private set; } = 20d;

    public double ConfirmedTwist { get; private set; } = 0d;

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        ConfirmedDistance = Math.Max(0.1d, (double)(DistanceInput.Value ?? 20m));
        ConfirmedTwist = (double)(TwistInput.Value ?? 0m);
        Close(new SweepFeatureDialogResult(ConfirmedDistance, ConfirmedTwist));
    }
}

public sealed record SweepFeatureDialogResult(double Distance, double TwistDegrees);
