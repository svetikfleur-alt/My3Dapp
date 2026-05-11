using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class OffsetSketchToolOptionsView : Avalonia.Controls.UserControl, ISketchToolOptionsView
{
    public OffsetSketchToolOptionsView()
    {
        InitializeComponent();
    }

    public double ToolParam
    {
        get
        {
            var input = this.FindControl<Avalonia.Controls.NumericUpDown>("DistanceInput");
            return input is not null ? (double)(input.Value ?? 1m) : 1d;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
