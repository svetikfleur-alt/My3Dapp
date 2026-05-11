using System.Collections.ObjectModel;
using System.Windows.Input;

namespace My3DApp.AvaloniaApp.ViewModels;

public class FeatureNode : ViewModelBase
{
    private string _icon = "⚙";
    public string Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    private string _featureType = string.Empty;
    public string FeatureType
    {
        get => _featureType;
        set => SetField(ref _featureType, value);
    }
    public ObservableCollection<FeatureNode> Children { get; } = new();

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    // Set when the node corresponds to a SceneGraph object
    public Guid? SceneObjectId { get; set; }

    // Human-readable solid parameters, e.g. "Box 80×8×50 mm @ (0,0,0)"
    // Shown in the properties panel and included in AI context.
    private string? _solidDescription;
    public string? SolidDescription
    {
        get => _solidDescription;
        set => SetField(ref _solidDescription, value);
    }

    // The viewer script that created this node's geometry.
    public string? ViewportAddScript { get; set; }

    // Commands set by the ViewModel when the node is created.
    // Keeping commands on the node avoids $parent traversal in compiled bindings.
    public ICommand? DeleteCommand          { get; set; }
    public ICommand? RenameCommand          { get; set; }
    public ICommand? ToggleVisibilityCommand { get; set; }
}
