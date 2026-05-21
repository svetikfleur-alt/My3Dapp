namespace My3DApp.AvaloniaApp.Services;

public sealed class StudioDocumentUiState
{
    public ProductProjectState? ProductProject { get; set; }

    public string ProjectId { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectType { get; set; } = "standard";

    public string ProjectCreatedAt { get; set; } = string.Empty;

    public string ProjectUpdatedAt { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = "partStudio";

    public string DocumentCreatedAt { get; set; } = string.Empty;

    public string DocumentUpdatedAt { get; set; } = string.Empty;

    public string ActiveWorkspaceKind { get; set; } = "PartStudio";

    public string SelectedTemplateId { get; set; } = string.Empty;

    public Dictionary<string, string> TemplateParameterValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<ExportJobState> ExportJobs { get; set; } = [];

    public List<string> AssistantNotes { get; set; } = [];
}

public sealed class ProductProjectState
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "standard";

    public string CreatedAt { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = string.Empty;

    public List<ProductDocumentState> Documents { get; set; } = [];
}

public sealed class ProductDocumentState
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "partStudio";

    public string CreatedAt { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = string.Empty;

    public List<ProductPartStudioState> PartStudios { get; set; } = [];
}

public sealed class ProductPartStudioState
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CreatedAt { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = string.Empty;

    public List<string> Bodies { get; set; } = [];

    public List<string> History { get; set; } = [];

    public string ActivePlane { get; set; } = "Top";

    public bool IsActive { get; set; }
}

public sealed class ExportJobState
{
    public string FileName { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public string Timestamp { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}
