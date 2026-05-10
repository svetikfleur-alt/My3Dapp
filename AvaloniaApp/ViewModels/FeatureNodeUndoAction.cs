using System.Collections.ObjectModel;
using My3DApp.AvaloniaApp.Services;
using My3DApp.Engine;

namespace My3DApp.AvaloniaApp.ViewModels;

/// <summary>
/// Combined undo action that keeps the feature tree UI and the engine SceneGraph in sync.
/// On Execute: adds a SceneObject and inserts the FeatureNode into the parent collection.
/// On Undo: removes both, and fires the viewport removal script.
/// </summary>
internal sealed class FeatureNodeUndoAction : IUndoableAction
{
    private readonly SceneGraph _scene;
    private readonly ObservableCollection<FeatureNode> _parent;
    private readonly FeatureNode _node;
    private readonly ViewportService? _viewport;
    private readonly string? _viewportAddScript;   // e.g. "viewer.addBox('N', 100,50,30); viewer.fitView();"
    private SceneObject? _sceneObject;

    public string Description => $"Add {_node.FeatureType} '{_node.Name}'";

    public FeatureNodeUndoAction(
        SceneGraph scene,
        ObservableCollection<FeatureNode> parent,
        FeatureNode node,
        ViewportService? viewport,
        string? viewportAddScript)
    {
        _scene           = scene;
        _parent          = parent;
        _node            = node;
        _viewport        = viewport;
        _viewportAddScript = viewportAddScript;
    }

    public void Execute()
    {
        _sceneObject  = _scene.Add(_node.Name, _node.FeatureType);
        _node.SceneObjectId = _sceneObject.Id;

        if (!_parent.Contains(_node))
            _parent.Add(_node);

        if (_viewportAddScript != null)
            _ = _viewport?.ExecuteScriptAsync(_viewportAddScript);
    }

    public void Undo()
    {
        if (_sceneObject != null)
            _scene.Remove(_sceneObject.Id);

        _parent.Remove(_node);

        _ = _viewport?.ExecuteScriptAsync(
            $"viewer.removeObject('{_node.Name.Replace("'", "")}'); viewer.fitView();");
    }
}
