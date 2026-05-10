using Avalonia.Controls;
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
        PlaneValueTextBlock.Text = string.IsNullOrWhiteSpace(planeSummary) ? "Sketch plane" : planeSummary;
        DistanceInput.Value = (decimal)initialDistance;
        SelectionStatusTextBlock.Text = ProfileValueTextBlock.Text.Contains("Select a closed", StringComparison.OrdinalIgnoreCase)
            ? "No valid profile selected."
            : "Closed profile selected.";
        RecipeTextBlock.Text = BuildRecipeText(CadExtrudeOperation.NewBody, initialDistance, false);

        // region: task 31 start
        foreach (var (id, name) in _bodies)
        {
            TargetBodyComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }

        if (_bodies.Count > 0)
        {
            var preferredIndex = -1;
            if (preferredTargetBodyId != Guid.Empty)
            {
                preferredIndex = _bodies
                    .Select((body, index) => body.Id == preferredTargetBodyId ? index : -1)
                    .FirstOrDefault(index => index >= 0);
            }

            TargetBodyComboBox.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
        }

        UpdateOperationUi(CadExtrudeOperation.NewBody);
        // region: task 31 end
    }

    public double ConfirmedDistance { get; private set; }

    public bool ReverseDirection { get; private set; }

    public CadExtrudeOperation SelectedOperation { get; private set; } = CadExtrudeOperation.NewBody;

    public Guid SelectedTargetBodyId { get; private set; }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var distance = (double)(DistanceInput.Value ?? 0m);
        ConfirmedDistance = distance;
        ReverseDirection = ReverseDirectionCheckBox.IsChecked == true;

        // region: task 31 start
        SelectedOperation = ResolveSelectedOperation();
        SelectedTargetBodyId = ResolveTargetBodyId();

        var signedDistance = ReverseDirection ? -distance : distance;
        Close(new ExtrudeFeatureDialogResult(
            signedDistance,
            ReverseDirection,
            SelectedOperation,
            SelectedTargetBodyId));
    }

    // region: task 31 start
    private void OnOperationChanged(object? sender, SelectionChangedEventArgs e)
    {
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
        OperationHelpTextBlock.Text = op switch
        {
            CadExtrudeOperation.Join => "Adds the new volume to an existing body.",
            CadExtrudeOperation.Cut => "Removes the extruded volume from the target body.",
            CadExtrudeOperation.Symmetric => "Extrudes equally on both sides of the sketch plane.",
            _ => "Creates a separate solid body."
        };
        RecipeTextBlock.Text = BuildRecipeText(op, (double)(DistanceInput.Value ?? 0m), ReverseDirectionCheckBox.IsChecked == true);
        ReverseDirectionCheckBox.IsEnabled = !symmetric;
        if (symmetric)
        {
            ReverseDirectionCheckBox.IsChecked = false;
        }

        for (var i = 0; i < OperationComboBox.Items.Count; i++)
        {
            if (OperationComboBox.Items[i] is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (tag == "Join" || tag == "Cut")
                {
                    item.IsEnabled = hasBodies;
                    Avalonia.Controls.ToolTip.SetTip(item, hasBodies ? null : "No bodies available");
                }
            }
        }
    }

    private CadExtrudeOperation ResolveSelectedOperation()
    {
        var selectedItem = OperationComboBox.SelectedItem as ComboBoxItem;
        return selectedItem?.Tag?.ToString() switch
        {
            "Join" => CadExtrudeOperation.Join,
            "Cut" => CadExtrudeOperation.Cut,
            "Symmetric" => CadExtrudeOperation.Symmetric,
            _ => CadExtrudeOperation.NewBody
        };
    }

    private Guid ResolveTargetBodyId()
    {
        if (TargetBodyComboBox.SelectedItem is ComboBoxItem item && item.Tag is Guid id)
            return id;
        if (TargetBodyComboBox.SelectedItem is ComboBoxItem item2 && item2.Tag is string s && Guid.TryParse(s, out var parsed))
            return parsed;
        return _bodies.Count > 0 ? _bodies[0].Id : Guid.Empty;
    }

    private static string BuildRecipeText(CadExtrudeOperation operation, double distance, bool reverse)
    {
        var opText = operation switch
        {
            CadExtrudeOperation.Join => "join",
            CadExtrudeOperation.Cut => "cut",
            CadExtrudeOperation.Symmetric => "symmetric",
            _ => "newBody"
        };
        var directionText = operation == CadExtrudeOperation.Symmetric
            ? "bothDirections"
            : reverse ? "reverse" : "forward";
        var depthText = Math.Abs(distance).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return $"extrude(profile, depth={depthText} mm, direction={directionText}, operation={opText})";
    }
}

public sealed record ExtrudeFeatureDialogResult(
    double Distance,
    bool ReverseDirection,
    CadExtrudeOperation Operation,
    Guid TargetBodyId);
