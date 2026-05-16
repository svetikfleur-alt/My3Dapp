using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FormaCore.Engine;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class ExtrudeFeatureDialog : AWindow
{
    private readonly IReadOnlyList<(Guid Id, string Name)> _bodies;

    public ExtrudeFeatureDialog()
    {
        _bodies = Array.Empty<(Guid, string)>();
        InitializeComponent();
    }

    public ExtrudeFeatureDialog(
        string profileSummary,
        string planeSummary,
        double initialDistance,
        IReadOnlyList<(Guid Id, string Name)>? bodies = null,
        Guid preferredTargetBodyId = default)
        : this()
    {
        _bodies = bodies ?? Array.Empty<(Guid, string)>();
        ProfileValueTextBlock.Text = string.IsNullOrWhiteSpace(profileSummary)
            ? "Select a closed sketch profile"
            : profileSummary;

        DistanceInput.Value = (decimal)initialDistance;

        foreach (var (id, name) in _bodies)
            TargetBodyComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });

        if (_bodies.Count > 0)
        {
            var preferredIndex = preferredTargetBodyId != Guid.Empty
                ? _bodies.Select((b, i) => b.Id == preferredTargetBodyId ? i : -1).FirstOrDefault(i => i >= 0)
                : 0;
            TargetBodyComboBox.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
        }

        UpdateOperationUi(CadExtrudeOperation.NewBody);
    }

    public double ConfirmedDistance { get; private set; }
    public bool ReverseDirection { get; private set; }
    public CadExtrudeOperation SelectedOperation { get; private set; } = CadExtrudeOperation.NewBody;
    public Guid SelectedTargetBodyId { get; private set; }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var distance = (double)(DistanceInput.Value ?? 0m);
        ConfirmedDistance = distance;
        ReverseDirection = ReverseDirectionCheckBox.IsChecked == true;
        SelectedOperation = ResolveSelectedOperation();
        SelectedTargetBodyId = ResolveTargetBodyId();
        var signedDistance = ReverseDirection ? -distance : distance;
        Close(new ExtrudeFeatureDialogResult(signedDistance, ReverseDirection, SelectedOperation, SelectedTargetBodyId));
    }

    private void OnOperationTabClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        if (clicked.IsChecked != true) { clicked.IsChecked = true; return; }
        foreach (var tab in new[] { OpNewTab, OpAddTab, OpRemoveTab, OpSymmetricTab })
            if (!ReferenceEquals(tab, clicked)) tab.IsChecked = false;
        UpdateOperationUi(ResolveSelectedOperation());
    }

    private void UpdateOperationUi(CadExtrudeOperation op)
    {
        var needsTarget = op == CadExtrudeOperation.Join || op == CadExtrudeOperation.Cut;
        var hasBodies = _bodies.Count > 0;
        var hasMultipleBodies = _bodies.Count > 1;
        var symmetric = op == CadExtrudeOperation.Symmetric;

        TargetBodyLabel.IsVisible = needsTarget && hasMultipleBodies;
        TargetBodyComboBox.IsVisible = needsTarget && hasMultipleBodies;
        NoBodyWarningText.IsVisible = needsTarget && !hasBodies;
        NoBodyWarningText.Text = op == CadExtrudeOperation.Cut
            ? "No bodies to cut. Create a body first."
            : "No bodies to join. Create a body first.";
        OkButton.IsEnabled = !needsTarget || hasBodies;

        ReverseDirectionCheckBox.IsEnabled = !symmetric;
        if (symmetric) ReverseDirectionCheckBox.IsChecked = false;

        OpAddTab.IsEnabled = hasBodies;
        OpRemoveTab.IsEnabled = hasBodies;
    }

    private CadExtrudeOperation ResolveSelectedOperation()
    {
        if (OpAddTab.IsChecked == true) return CadExtrudeOperation.Join;
        if (OpRemoveTab.IsChecked == true) return CadExtrudeOperation.Cut;
        if (OpSymmetricTab.IsChecked == true) return CadExtrudeOperation.Symmetric;
        return CadExtrudeOperation.NewBody;
    }

    private Guid ResolveTargetBodyId()
    {
        if (TargetBodyComboBox.SelectedItem is ComboBoxItem item && item.Tag is Guid id)
            return id;
        if (TargetBodyComboBox.SelectedItem is ComboBoxItem item2 && item2.Tag is string s && Guid.TryParse(s, out var parsed))
            return parsed;
        return _bodies.Count > 0 ? _bodies[0].Id : Guid.Empty;
    }
}

public sealed record ExtrudeFeatureDialogResult(
    double Distance,
    bool ReverseDirection,
    CadExtrudeOperation Operation,
    Guid TargetBodyId);
