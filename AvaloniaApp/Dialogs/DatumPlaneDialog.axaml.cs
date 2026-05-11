using Avalonia.Controls;
using Avalonia.Interactivity;
using FormaCore.Engine;

namespace My3DApp.AvaloniaApp.Dialogs;

public partial class DatumPlaneDialog : Window
{
    public string ResultPlaneName { get; private set; } = string.Empty;
    public CadReferencePlaneKind ResultSourceKind { get; private set; } = CadReferencePlaneKind.Top;
    public double ResultOffset { get; private set; }
    public bool Confirmed { get; private set; }

    public DatumPlaneDialog()
    {
        InitializeComponent();
        SourcePlaneCombo.SelectedIndex = 0;
        UpdateAutoName();
        OffsetInput.ValueChanged += (_, _) => UpdateAutoName();
        SourcePlaneCombo.SelectionChanged += (_, _) => UpdateAutoName();
    }

    private void UpdateAutoName()
    {
        var src = SelectedSource();
        var offset = (double)(OffsetInput.Value ?? 10);
        NameInput.Text = $"Offset {src} +{offset:0.##} mm";
    }

    private string SelectedSource()
    {
        var item = SourcePlaneCombo.SelectedItem as ComboBoxItem;
        return item?.Tag?.ToString() ?? "Top";
    }

    private void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        ResultSourceKind = SelectedSource() switch
        {
            "Front" => CadReferencePlaneKind.Front,
            "Right" => CadReferencePlaneKind.Right,
            _       => CadReferencePlaneKind.Top
        };
        ResultOffset = (double)(OffsetInput.Value ?? 10);
        ResultPlaneName = string.IsNullOrWhiteSpace(NameInput.Text)
            ? $"Offset {ResultSourceKind} +{ResultOffset:0.##} mm"
            : NameInput.Text.Trim();
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
