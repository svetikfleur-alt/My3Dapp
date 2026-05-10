using My3DApp.Core;

namespace My3DApp.Engine;

public interface IUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Classic command-pattern undo/redo stack. Thread-safe for UI dispatch.
/// </summary>
public class UndoRedoStack
{
    private readonly Stack<IUndoableAction> _undo = new();
    private readonly Stack<IUndoableAction> _redo = new();
    private readonly int _maxDepth;

    public UndoRedoStack(int maxDepth = 100) => _maxDepth = maxDepth;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? NextUndoDescription => CanUndo ? _undo.Peek().Description : null;
    public string? NextRedoDescription => CanRedo ? _redo.Peek().Description : null;

    public event Action? StackChanged;

    public void Push(IUndoableAction action)
    {
        action.Execute();
        _undo.Push(action);
        _redo.Clear();

        // Trim beyond max depth
        while (_undo.Count > _maxDepth)
        {
            var items = _undo.ToArray();
            _undo.Clear();
            foreach (var item in items.Take(_maxDepth).Reverse())
                _undo.Push(item);
        }

        RuntimeLog.Info("Undo", $"Push '{action.Description}' (depth={_undo.Count})");
        StackChanged?.Invoke();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        var action = _undo.Pop();
        action.Undo();
        _redo.Push(action);
        RuntimeLog.Info("Undo", $"Undo '{action.Description}'");
        StackChanged?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        var action = _redo.Pop();
        action.Execute();
        _undo.Push(action);
        RuntimeLog.Info("Undo", $"Redo '{action.Description}'");
        StackChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        StackChanged?.Invoke();
    }
}

// ── Concrete actions ──────────────────────────────────────────────────────────

public class AddSceneObjectAction : IUndoableAction
{
    private readonly SceneGraph _scene;
    private readonly string _name;
    private readonly string _type;
    private SceneObject? _created;

    public string Description => $"Add {_type} '{_name}'";

    public AddSceneObjectAction(SceneGraph scene, string name, string type)
    {
        _scene = scene;
        _name = name;
        _type = type;
    }

    public void Execute() => _created = _scene.Add(_name, _type);
    public void Undo()
    {
        if (_created != null) _scene.Remove(_created.Id);
    }
}

public class RemoveSceneObjectAction : IUndoableAction
{
    private readonly SceneGraph _scene;
    private readonly SceneObject _object;

    public string Description => $"Remove '{_object.Name}'";

    public RemoveSceneObjectAction(SceneGraph scene, SceneObject obj)
    {
        _scene = scene;
        _object = obj;
    }

    public void Execute() => _scene.Remove(_object.Id);
    public void Undo()    => _scene.Add(_object.Name, _object.Type);
}
