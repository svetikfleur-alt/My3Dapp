using System.Globalization;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class NumericFeatureDialog : AWindow
{
    public NumericFeatureDialog()
    {
        InitializeComponent();
    }

    public NumericFeatureDialog(
        string title,
        string subtitle,
        string valueLabel,
        double initialValue,
        double minimum,
        double maximum,
        string unit)
        : this()
    {
        Title = title;
        TitleTextBlock.Text = title;
        SubtitleTextBlock.Text = subtitle;
        ValueLabelTextBlock.Text = valueLabel;
        ValueInput.Minimum = (decimal)minimum;
        ValueInput.Maximum = (decimal)maximum;
        ValueInput.Value = (decimal)initialValue;
        UnitTextBlock.Text = unit;
    }

    public double ConfirmedValue { get; private set; }

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
        ConfirmedValue = (double)(ValueInput.Value ?? 0m);
        Close(ConfirmedValue.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
