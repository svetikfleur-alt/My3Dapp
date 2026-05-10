using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Body content for the Rectangle sketch tool dialog. Hosted inside <see cref="ToolDialogWindow"/>.
/// </summary>
public sealed partial class RectangleSketchToolOptionsView : Avalonia.Controls.UserControl
{
    public RectangleSketchToolOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Returns true if center-to-corner mode is selected.</summary>
    public bool IsCenterMode => CenterModeButton?.IsChecked == true;
}
