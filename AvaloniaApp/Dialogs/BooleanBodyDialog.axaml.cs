using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class BooleanBodyDialog : AWindow
{
    private readonly IReadOnlyList<(Guid Id, string Name)> _bodies;

    public BooleanBodyDialog()
    {
        InitializeComponent();
        _bodies = [];
    }

    public BooleanBodyDialog(
        IReadOnlyList<(Guid Id, string Name)> bodies,
        Guid initialBodyAId,
        Guid initialBodyBId,
        string initialOperation)
        : this()
    {
        _bodies = bodies;

        foreach (var (_, name) in bodies)
        {
            BodyAComboBox.Items.Add(name);
            BodyBComboBox.Items.Add(name);
        }

        var indexA = bodies.ToList().FindIndex(b => b.Id == initialBodyAId);
        var indexB = bodies.ToList().FindIndex(b => b.Id == initialBodyBId);

        BodyAComboBox.SelectedIndex = indexA >= 0 ? indexA : 0;
        BodyBComboBox.SelectedIndex = indexB >= 0 ? indexB : (bodies.Count > 1 ? 1 : 0);

        switch (initialOperation.ToLowerInvariant())
        {
            case "subtract":
                SubtractRadio.IsChecked = true;
                break;
            case "intersect":
                IntersectRadio.IsChecked = true;
                break;
            default:
                UnionRadio.IsChecked = true;
                break;
        }
    }

    public BooleanBodyDialogResult? Result { get; private set; }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var indexA = BodyAComboBox.SelectedIndex;
        var indexB = BodyBComboBox.SelectedIndex;

        if (indexA < 0 || indexA >= _bodies.Count || indexB < 0 || indexB >= _bodies.Count)
        {
            Close(null);
            return;
        }

        var bodyAId = _bodies[indexA].Id;
        var bodyBId = _bodies[indexB].Id;

        var operation = SubtractRadio.IsChecked == true ? "Subtract"
            : IntersectRadio.IsChecked == true ? "Intersect"
            : "Union";

        Result = new BooleanBodyDialogResult(bodyAId, bodyBId, operation);
        Close(Result);
    }
}

public sealed record BooleanBodyDialogResult(Guid BodyAId, Guid BodyBId, string Operation);
