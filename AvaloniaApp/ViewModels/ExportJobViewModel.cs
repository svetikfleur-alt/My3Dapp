namespace My3DApp.AvaloniaApp.ViewModels;

public sealed record ExportJobViewModel(
    string FileName,
    string Format,
    string Scope,
    string Timestamp,
    string FullPath);
