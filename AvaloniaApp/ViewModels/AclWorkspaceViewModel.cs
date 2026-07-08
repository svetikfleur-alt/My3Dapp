using System.Collections.ObjectModel;
using FormaCore.Engine.Acl;
using FormaCore.Engine.Exact;
using FormaCore.Engine.Services;

namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class AclWorkspaceViewModel : ViewModelBase, IDisposable
{
    private readonly IAclDocumentService _documentService;
    private readonly AclBuildCoordinator _buildCoordinator;
    private string _sourceText = string.Empty;
    private string _buildStatus = "Ready";
    private bool _isStale;

    public AclWorkspaceViewModel()
    {
        _documentService = new AclDocumentService();
        _buildCoordinator = new AclBuildCoordinator();
        Diagnostics = new ObservableCollection<AclDiagnosticViewModel>();
        
        // Setup initial default document
        _documentService.NewDocument(
            "let width = 120mm;\nlet depth = 80mm;\nlet height = 18mm;\n\npart plate {\n    box(width: width, depth: depth, height: height);\n}\n");
        _sourceText = _documentService.CurrentContent;
    }

    public ObservableCollection<AclDiagnosticViewModel> Diagnostics { get; }

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (SetProperty(ref _sourceText, value))
            {
                _documentService.UpdateContent(value);
                RaisePropertyChanged(nameof(IsDirty));
                RaisePropertyChanged(nameof(DocumentTitle));
            }
        }
    }

    public string BuildStatus
    {
        get => _buildStatus;
        private set => SetProperty(ref _buildStatus, value);
    }

    public bool IsStale
    {
        get => _isStale;
        private set
        {
            if (SetProperty(ref _isStale, value))
            {
                RaisePropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    public string StatusDisplay => IsStale ? "STALE — BUILD FAILED" : BuildStatus;

    public bool IsDirty => _documentService.IsDirty;
    
    public string DocumentTitle => 
        (_documentService.HasFile ? Path.GetFileName(_documentService.CurrentFilePath) : "Untitled.acl") + 
        (IsDirty ? "*" : "");

    public void OpenFile(string path)
    {
        _documentService.OpenDocument(path);
        SourceText = _documentService.CurrentContent;
        Build();
    }

    public void SaveFile()
    {
        _documentService.UpdateContent(SourceText);
        _documentService.SaveDocument();
        RaisePropertyChanged(nameof(IsDirty));
        RaisePropertyChanged(nameof(DocumentTitle));
    }

    public void SaveFileAs(string path)
    {
        _documentService.UpdateContent(SourceText);
        _documentService.SaveDocumentAs(path);
        RaisePropertyChanged(nameof(IsDirty));
        RaisePropertyChanged(nameof(DocumentTitle));
    }

    public void Build()
    {
        _documentService.UpdateContent(SourceText);
        BuildStatus = "Building...";
        
        var (success, diagnostics, graph) = _buildCoordinator.Build(_documentService.CurrentContent);
        
        Diagnostics.Clear();
        foreach (var diag in diagnostics)
        {
            Diagnostics.Add(new AclDiagnosticViewModel(diag));
        }

        IsStale = !success;
        BuildStatus = success ? "Build Successful" : "Build Failed";
        
        BuildCompleted?.Invoke(this, graph);
    }

    public AclBuildCoordinator BuildCoordinator => _buildCoordinator;

    public FormaCore.Engine.Exact.Graph.ExactFeatureGraph? ComputePreview(string tempSource)
    {
        return _buildCoordinator.ComputePreview(tempSource);
    }

    public event EventHandler<FormaCore.Engine.Exact.Graph.ExactFeatureGraph?>? BuildCompleted;

    public void Dispose()
    {
        // cleanup if needed
    }
}
