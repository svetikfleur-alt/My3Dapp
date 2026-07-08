using FormaCore.Engine.Acl;

namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class AclDiagnosticViewModel : ViewModelBase
{
    private readonly AclDiagnostic _diagnostic;

    public AclDiagnosticViewModel(AclDiagnostic diagnostic)
    {
        _diagnostic = diagnostic;
    }

    public string Severity => _diagnostic.Severity.ToString();
    public string Category => _diagnostic.Category;
    public string Message => _diagnostic.Message;
    public string Location => _diagnostic.Span.ToString();
    
    public int StartLine => _diagnostic.Span.StartLine;
    public int StartColumn => _diagnostic.Span.StartColumn;
    
    public AclDiagnostic Diagnostic => _diagnostic;
}
