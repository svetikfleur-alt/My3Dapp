using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Body content for the Point sketch tool dialog. Hosted inside <see cref="ToolDialogWindow"/>.
/// </summary>
public sealed partial class PointSketchToolOptionsView : Avalonia.Controls.UserControl
{
    public PointSketchToolOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Returns X coordinate for direct-entry mode (0 = click mode).</summary>
    public double EntryX => (double)(XInput?.Value ?? 0m);

    /// <summary>Returns Y coordinate for direct-entry mode (0 = click mode).</summary>
    public double EntryY => (double)(YInput?.Value ?? 0m);

    /// <summary>True if both X and Y are non-zero — caller should auto-commit the point.</summary>
    public bool HasDirectEntry => EntryX != 0d || EntryY != 0d;
}
