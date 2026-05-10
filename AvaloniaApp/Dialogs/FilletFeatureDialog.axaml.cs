using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class FilletFeatureDialog : AWindow
{
    public FilletFeatureDialog()
    {
        InitializeComponent();
    }

    public FilletFeatureDialog(string selectionSummary, double initialRadius)
        : this()
    {
        SelectionValueTextBlock.Text = string.IsNullOrWhiteSpace(selectionSummary)
            ? "Select a body"
            : selectionSummary;
        RadiusInput.Value = (decimal)Math.Clamp(initialRadius, 0.1d, 100d);
    }

    public double ConfirmedRadius { get; private set; } = 2d;

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
        ConfirmedRadius = Math.Clamp((double)(RadiusInput.Value ?? 2m), 0.1d, 100d);
        Close(new FilletFeatureDialogResult(ConfirmedRadius));
    }
}

public sealed record FilletFeatureDialogResult(double Radius);
