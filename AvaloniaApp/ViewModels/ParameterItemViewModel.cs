namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class ParameterItemViewModel : ViewModelBase
{
    private string _value;
    private string _validationMessage = string.Empty;
    private bool _isValueValid = true;

    public ParameterItemViewModel(
        string name,
        string key,
        string value,
        bool isEditable,
        string unit,
        double minValue,
        double maxValue,
        string description)
    {
        Name = name;
        Key = key;
        _value = value;
        IsEditable = isEditable;
        Unit = unit;
        MinValue = minValue;
        MaxValue = maxValue;
        Description = description;
    }

    public string Name { get; }

    public string Key { get; }

    public bool IsEditable { get; }

    public bool IsReadOnly => !IsEditable;

    public string Unit { get; }

    public double MinValue { get; }

    public double MaxValue { get; }

    public string Description { get; }

    public string RangeLabel => $"{MinValue:0.###} to {MaxValue:0.###} {Unit}".Trim();

    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                RaisePropertyChanged(nameof(HasValidationMessage));
                RaisePropertyChanged(nameof(HasNoValidationMessage));
            }
        }
    }

    public bool IsValueValid
    {
        get => _isValueValid;
        set => SetProperty(ref _isValueValid, value);
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool HasNoValidationMessage => !HasValidationMessage;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
