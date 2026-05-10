using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class ExportDialog : AWindow
{
    private readonly Func<string, bool, string, Task<string?>> _exportAction = null!;

    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(bool hasSelection, Func<string, bool, string, Task<string?>> exportAction)
        : this()
    {
        _exportAction = exportAction;

        if (!hasSelection)
        {
            SelectedBodyRadio.IsEnabled = false;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnFormatChanged(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PathBox?.Text))
            return;

        var ext = ObjRadio?.IsChecked == true ? ".obj" : ".stl";
        PathBox.Text = System.IO.Path.ChangeExtension(PathBox.Text, ext);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var isObj = ObjRadio?.IsChecked == true;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = isObj ? "Export OBJ" : "Export STL",
            SuggestedFileName = isObj ? "model.obj" : "model.stl",
            FileTypeChoices =
            [
                new FilePickerFileType(isObj ? "OBJ File" : "STL File")
                {
                    Patterns = [isObj ? "*.obj" : "*.stl"]
                }
            ]
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            PathBox.Text = path;
            ExportButton.IsEnabled = true;
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var path = PathBox?.Text;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var format = ObjRadio?.IsChecked == true ? "OBJ" : "STL";
        var allBodies = SelectedBodyRadio?.IsChecked != true;

        ExportButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        if (ErrorLabel is not null)
            ErrorLabel.IsVisible = false;

        var error = await _exportAction(format, allBodies, path);

        if (error is null)
        {
            Close(true);
        }
        else
        {
            ExportButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            if (ErrorLabel is not null)
            {
                ErrorLabel.Text = error;
                ErrorLabel.IsVisible = true;
            }
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
