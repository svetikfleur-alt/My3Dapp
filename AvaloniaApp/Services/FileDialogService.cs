using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp.Services;

public interface IFileDialogService
{
    Task<string?> OpenFileAsync(string title, params FilePickerFileType[] filters);
    Task<string?> SaveFileAsync(string title, string defaultName, params FilePickerFileType[] filters);
    Task<string?> ShowRenameDialogAsync(string currentName);
    Task<FeatureDialogResult> ShowFeatureDialogAsync(string featureType);
}

public class FileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public FileDialogService(Window owner) => _owner = owner;

    public async Task<string?> OpenFileAsync(string title, params FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string title, string defaultName, params FilePickerFileType[] filters)
    {
        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices = filters
        });

        return file?.TryGetLocalPath();
    }

    public Task<FeatureDialogResult> ShowFeatureDialogAsync(string featureType)
        => FeatureDialog.ShowAsync(_owner, featureType);

    public async Task<string?> ShowRenameDialogAsync(string currentName)
    {
        var tcs = new TaskCompletionSource<string?>();

        var input = new TextBox
        {
            Text = currentName,
            SelectionStart = 0,
            SelectionEnd = currentName.Length,
            Background = new SolidColorBrush(Color.Parse("#0B1929")),
            Foreground = new SolidColorBrush(Color.Parse("#C8D8E8")),
            CaretBrush = new SolidColorBrush(Color.Parse("#4B9EF5")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4),
        };

        var okBtn = new Button
        {
            Content = "Rename",
            Padding = new Thickness(16, 6),
            Background = new SolidColorBrush(Color.Parse("#4F46E5")),
            Foreground = new SolidColorBrush(Colors.White),
        };
        var cancelBtn = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 6),
            Background = new SolidColorBrush(Color.Parse("#1A304D")),
            Foreground = new SolidColorBrush(Color.Parse("#A0BCD8")),
        };

        void Confirm() { var t = input.Text?.Trim(); tcs.TrySetResult(string.IsNullOrEmpty(t) ? null : t); }
        void Cancel()  { tcs.TrySetResult(null); }

        okBtn.Click    += (_, _) => Confirm();
        cancelBtn.Click += (_, _) => Cancel();

        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)  { Confirm(); e.Handled = true; }
            if (e.Key == Key.Escape) { Cancel();  e.Handled = true; }
        };

        var dialog = new Window
        {
            Title = "Rename Feature",
            Width = 320,
            Height = 130,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#111827")),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { okBtn, cancelBtn }
                    }
                }
            }
        };

        dialog.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        dialog.Closed  += (_, _) => tcs.TrySetResult(null);

        await dialog.ShowDialog(_owner);
        return await tcs.Task;
    }

    // ── Preset file type filters ──────────────────────────────────────────────

    public static readonly FilePickerFileType Stl = new("STL Mesh")
    {
        Patterns = ["*.stl"],
        MimeTypes = ["model/stl", "application/octet-stream"]
    };

    public static readonly FilePickerFileType Obj = new("OBJ Mesh")
    {
        Patterns = ["*.obj"],
        MimeTypes = ["model/obj"]
    };

    public static readonly FilePickerFileType Step = new("STEP / STP")
    {
        Patterns = ["*.step", "*.stp"],
        MimeTypes = ["model/step", "application/octet-stream"]
    };

    public static readonly FilePickerFileType ThreeMf = new("3MF")
    {
        Patterns = ["*.3mf"],
        MimeTypes = ["model/3mf"]
    };

    public static readonly FilePickerFileType AllCad = new("All Supported CAD")
    {
        Patterns = ["*.stl", "*.obj", "*.step", "*.stp", "*.3mf"]
    };
}
