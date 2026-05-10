using System.Collections.ObjectModel;
using My3DApp.AvaloniaApp.Services;
using My3DApp.Engine;

namespace My3DApp.AvaloniaApp.ViewModels;

/// <summary>
/// Undo action for right-click → Delete. Removes the FeatureNode and its SceneObject;
/// on undo, re-inserts the node at its original index and restores the viewport geometry.
/// </summary>
internal sealed class DeleteFeatureNodeAction : IUndoableAction
{
    private readonly SceneGraph _scene;
    private readonly ObservableCollection<FeatureNode> _parent;
    private readonly FeatureNode _node;
    private readonly int _originalIndex;
    private readonly ViewportService? _viewport;

    public string Description => $"Delete '{_node.Name}'";

    public DeleteFeatureNodeAction(
        SceneGraph scene,
        ObservableCollection<FeatureNode> parent,
        FeatureNode node,
        int originalIndex,
        ViewportService? viewport)
    {
        _scene         = scene;
        _parent        = parent;
        _node          = node;
        _originalIndex = originalIndex;
        _viewport      = viewport;
    }

    public void Execute()
    {
        if (_node.SceneObjectId.HasValue)
            _scene.Remove(_node.SceneObjectId.Value);

        _parent.Remove(_node);

        _ = _viewport?.ExecuteScriptAsync(
            $"viewer.removeObject('{_node.Name.Replace("'", "")}');");
    }

    public void Undo()
    {
        // Re-create scene object and re-insert the node at its original position
        var obj = _scene.Add(_node.Name, _node.FeatureType);
        _node.SceneObjectId = obj.Id;

        var insertAt = Math.Min(_originalIndex, _parent.Count);
        _parent.Insert(insertAt, _node);

        // Replay the add script to restore viewport geometry
        if (_node.ViewportAddScript is { } script)
            _ = _viewport?.ExecuteScriptAsync(script);
    }
}
