using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Body content for the Circle sketch tool dialog. Hosted inside <see cref="ToolDialogWindow"/>.
/// </summary>
public sealed partial class CircleSketchToolOptionsView : Avalonia.Controls.UserControl
{
    public CircleSketchToolOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Returns true if 3-point mode is selected.</summary>
    public bool IsThreePointMode => ThreePointButton?.IsChecked == true;
}
