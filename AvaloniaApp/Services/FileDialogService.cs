using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace My3DApp.AvaloniaApp.Services;

public interface IFileDialogService
{
    Task<string?> OpenFileAsync(string title, params FilePickerFileType[] filters);
    Task<string?> SaveFileAsync(string title, string defaultName, params FilePickerFileType[] filters);
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
