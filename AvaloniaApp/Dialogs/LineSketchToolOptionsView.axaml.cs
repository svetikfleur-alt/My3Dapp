using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Body content for the Line sketch tool dialog. Hosted inside <see cref="ToolDialogWindow"/>.
/// First-cut: descriptive only. Future option groups (snap, length input, chain on/off)
/// can be added here without touching the dialog scaffold.
/// </summary>
public sealed partial class LineSketchToolOptionsView : Avalonia.Controls.UserControl
{
    public LineSketchToolOptionsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}