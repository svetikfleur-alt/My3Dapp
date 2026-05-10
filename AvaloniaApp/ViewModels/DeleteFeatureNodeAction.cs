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

    private SceneObject? _removed; // stored so Undo can restore it with its Solid intact

    public void Execute()
    {
        if (_node.SceneObjectId.HasValue)
        {
            _removed = _scene.Find(_node.SceneObjectId.Value);
            _scene.Remove(_node.SceneObjectId.Value);
        }

        _parent.Remove(_node);

        _ = _viewport?.ExecuteScriptAsync(
            $"viewer.removeObject('{_node.Name.Replace("'", "")}');");
    }

    public void Undo()
    {
        // Restore the original SceneObject (preserving its Solid definition)
        if (_removed != null)
            _scene.AddExisting(_removed);

        var insertAt = Math.Min(_originalIndex, _parent.Count);
        _parent.Insert(insertAt, _node);

        if (_node.ViewportAddScript is { } script)
            _ = _viewport?.ExecuteScriptAsync(script);
    }
}
