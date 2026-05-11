using Avalonia.Controls;
using Avalonia.Input;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp;

public partial class FeatureDialog : Window
{
    private FeatureDialogResult _result = new();

    public FeatureDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public static async Task<FeatureDialogResult> ShowAsync(Window owner, string featureType)
    {
        var vm = new FeatureDialogViewModel(featureType);
        var dialog = new FeatureDialog
        {
            DataContext = vm
        };

        // Wire OK / Cancel after InitializeComponent so named controls exist
        var okBtn     = dialog.FindControl<Button>("OkBtn");
        var cancelBtn = dialog.FindControl<Button>("CancelBtn");

        if (okBtn     != null) okBtn.Click     += (_, _) => dialog.Confirm(vm);
        if (cancelBtn != null) cancelBtn.Click += (_, _) => dialog.Cancel();

        await dialog.ShowDialog(owner);
        return dialog._result;
    }

    private void Confirm(FeatureDialogViewModel vm)
    {
        _result = vm.ToResult(true);
        Close();
    }

    private void Cancel()
    {
        _result = new FeatureDialogResult { Confirmed = false };
        Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Cancel(); e.Handled = true; }
        if (e.Key == Key.Enter && DataContext is FeatureDialogViewModel vm)
        {
            Confirm(vm);
            e.Handled = true;
        }
    }
}
