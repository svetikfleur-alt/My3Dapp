using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class Fillet2dSketchToolOptionsView : Avalonia.Controls.UserControl, ISketchToolOptionsView
{
    public Fillet2dSketchToolOptionsView()
    {
        InitializeComponent();
    }

    public double ToolParam
    {
        get
        {
            var input = this.FindControl<Avalonia.Controls.NumericUpDown>("RadiusInput");
            return input is not null ? (double)(input.Value ?? 1m) : 1d;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
