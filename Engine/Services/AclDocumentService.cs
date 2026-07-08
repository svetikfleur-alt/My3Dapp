using System.Text;

namespace FormaCore.Engine.Services;

public sealed class AclDocumentService : IAclDocumentService
{
    private string _currentFilePath = string.Empty;
    private string _currentContent = string.Empty;
    private bool _isDirty = false;

    public string CurrentFilePath => _currentFilePath;
    public string CurrentContent => _currentContent;
    public bool IsDirty => _isDirty;
    public bool HasFile => !string.IsNullOrEmpty(_currentFilePath);

    public void NewDocument(string initialContent = "")
    {
        _currentFilePath = string.Empty;
        _currentContent = initialContent;
        _isDirty = !string.IsNullOrEmpty(initialContent);
    }

    public void UpdateContent(string content)
    {
        if (_currentContent != content)
        {
            _currentContent = content;
            _isDirty = true;
        }
    }

    public void OpenDocument(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("ACL file not found.", path);
        }

        _currentContent = File.ReadAllText(path, Encoding.UTF8);
        _currentFilePath = path;
        _isDirty = false;
    }

    public void SaveDocument()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            throw new InvalidOperationException("No file path set. Use SaveDocumentAs first.");
        }

        File.WriteAllText(_currentFilePath, _currentContent, Encoding.UTF8);
        _isDirty = false;
    }

    public void SaveDocumentAs(string path)
    {
        _currentFilePath = path;
        SaveDocument();
    }
}
