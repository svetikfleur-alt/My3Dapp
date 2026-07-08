namespace FormaCore.Engine.Services;

public interface IAclDocumentService
{
    string CurrentFilePath { get; }
    string CurrentContent { get; }
    bool IsDirty { get; }
    bool HasFile { get; }
    
    void NewDocument(string initialContent = "");
    void UpdateContent(string content);
    void OpenDocument(string path);
    void SaveDocument();
    void SaveDocumentAs(string path);
}
