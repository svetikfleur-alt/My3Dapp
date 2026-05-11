using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AUserControl = Avalonia.Controls.UserControl;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class PolygonSketchToolOptionsView : AUserControl, ISketchToolOptionsView
{
    public PolygonSketchToolOptionsView()
    {
        InitializeComponent();
    }

    public double ToolParam
    {
        get
        {
            var input = this.FindControl<Avalonia.Controls.NumericUpDown>("SidesInput");
            return input is not null ? (double)(input.Value ?? 6m) : 6d;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
