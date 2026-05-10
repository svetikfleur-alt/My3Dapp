using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Body content for the Arc sketch tool dialog. Hosted inside <see cref="ToolDialogWindow"/>.
/// </summary>
public sealed partial class ArcSketchToolOptionsView : Avalonia.Controls.UserControl
{
    public ArcSketchToolOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Returns true if center-start-end mode is selected.</summary>
    public bool IsCenterStartEndMode => CenterStartEndButton?.IsChecked == true;
}
