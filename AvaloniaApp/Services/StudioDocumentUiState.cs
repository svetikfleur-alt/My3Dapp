namespace My3DApp.AvaloniaApp.Services;

public sealed class StudioDocumentUiState
{
    public string ActiveWorkspaceKind { get; set; } = "PartStudio";

    public string SelectedTemplateId { get; set; } = string.Empty;

    public Dictionary<string, string> TemplateParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ExportJobState> ExportJobs { get; set; } = [];

    public List<string> AssistantNotes { get; set; } = [];
}

public sealed class ExportJobState
{
    public string FileName { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Timestamp { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}
