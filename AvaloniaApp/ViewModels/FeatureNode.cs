using System.Collections.ObjectModel;

namespace My3DApp.AvaloniaApp.ViewModels;

public class FeatureNode : ViewModelBase
{
    public string Icon { get; set; } = "⚙";
    public string Name { get; set; } = string.Empty;
    public string FeatureType { get; set; } = string.Empty;
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
}
