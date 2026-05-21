using System.Collections.ObjectModel;

namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class RecipeStepViewModel : ViewModelBase
{
    private string _status = "pending";
    private string _error = string.Empty;

    public RecipeStepViewModel(string id, string operation, string parameters, string commandText)
    {
        Id = id;
        Operation = operation;
        Parameters = parameters;
        CommandText = commandText;
    }

    public string Id { get; }

    public string Operation { get; }

    public string Parameters { get; }

    public string CommandText { get; }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Error
    {
        get => _error;
        set
        {
            if (SetProperty(ref _error, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
}

public sealed class CadRecipeRunViewModel : ViewModelBase
{
    private string _statusSummary = "Recipe ready to validate.";

    public CadRecipeRunViewModel(
        string recipeId,
        string title,
        string units,
        string description,
        string parameterSummary,
        string exportHints,
        string commandText)
    {
        RecipeId = recipeId;
        Title = title;
        Units = units;
        Description = description;
        ParameterSummary = parameterSummary;
        ExportHints = exportHints;
        CommandText = commandText;
    }

    public string RecipeId { get; }

    public string Title { get; }

    public string Units { get; }

    public string Description { get; }

    public string ParameterSummary { get; }

    public string ExportHints { get; }

    public string CommandText { get; }

    public ObservableCollection<RecipeStepViewModel> Steps { get; } = [];

    public string StatusSummary
    {
        get => _statusSummary;
        set => SetProperty(ref _statusSummary, value);
    }
}
