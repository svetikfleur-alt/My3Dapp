using Avalonia.Controls;
using Avalonia.Interactivity;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed record LoftSketchItem(Guid Id, string Name);

public sealed partial class LoftFeatureDialog : AWindow
{
    private IReadOnlyList<LoftSketchItem> _sketches = [];

    public LoftFeatureDialog()
    {
        InitializeComponent();
    }

    public LoftFeatureDialog(
        IReadOnlyList<(Guid Id, string Name)> sketches,
        Guid initialProfileAId,
        Guid initialProfileBId,
        double initialDistance)
        : this()
    {
        _sketches = sketches.Select(s => new LoftSketchItem(s.Id, s.Name)).ToList();
        var names = _sketches.Select(s => s.Name).ToArray();
        ProfileAComboBox.ItemsSource = names;
        ProfileBComboBox.ItemsSource = names;

        if (_sketches.Count == 0)
        {
            ProfileHintLabel.Text = "No closed sketch profiles found. Create and finish at least two sketch profiles first.";
            OkButton.IsEnabled = false;
        }
        else if (_sketches.Count < 2)
        {
            ProfileHintLabel.Text = "At least two closed sketch profiles are needed for loft.";
            OkButton.IsEnabled = false;
        }

        var aIdx = _sketches.ToList().FindIndex(s => s.Id == initialProfileAId);
        var bIdx = _sketches.ToList().FindIndex(s => s.Id == initialProfileBId);
        ProfileAComboBox.SelectedIndex = aIdx >= 0 ? aIdx : 0;
        ProfileBComboBox.SelectedIndex = bIdx >= 0 ? bIdx : Math.Min(1, _sketches.Count - 1);

        DistanceInput.Value = (decimal)Math.Max(0.1d, initialDistance);
    }

    public Guid ConfirmedProfileAId { get; private set; }

    public Guid ConfirmedProfileBId { get; private set; }

    public double ConfirmedDistance { get; private set; } = 20d;

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var aIdx = ProfileAComboBox.SelectedIndex;
        var bIdx = ProfileBComboBox.SelectedIndex;

        if (aIdx < 0 || bIdx < 0 || aIdx >= _sketches.Count || bIdx >= _sketches.Count || aIdx == bIdx)
        {
            ProfileHintLabel.Text = "Please select two different sketch profiles.";
            return;
        }

        ConfirmedProfileAId = _sketches[aIdx].Id;
        ConfirmedProfileBId = _sketches[bIdx].Id;
        ConfirmedDistance = Math.Max(0.1d, (double)(DistanceInput.Value ?? 20m));
        Close(new LoftFeatureDialogResult(ConfirmedProfileAId, ConfirmedProfileBId, ConfirmedDistance));
    }
}

public sealed record LoftFeatureDialogResult(Guid ProfileAId, Guid ProfileBId, double Distance);
