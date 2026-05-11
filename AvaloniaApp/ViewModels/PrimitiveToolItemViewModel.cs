namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class PrimitiveToolItemViewModel
{
    public PrimitiveToolItemViewModel(string kind, string name, string iconPath)
    {
        Kind = kind;
        Name = name;
        IconPath = iconPath;
    }

    public string Kind { get; }

    public string Name { get; }

    public string IconPath { get; }
}
