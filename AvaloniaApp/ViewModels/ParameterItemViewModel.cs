namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class ParameterItemViewModel : ViewModelBase
{
    private string _value;

    public ParameterItemViewModel(string name, string key, string value, bool isEditable)
    {
        Name = name;
        Key = key;
        _value = value;
        IsEditable = isEditable;
    }

    public string Name { get; }

    public string Key { get; }

    public bool IsEditable { get; }

    public bool IsReadOnly => !IsEditable;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
