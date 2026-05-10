using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class SelectPlaneDialog : AWindow
{
    private readonly IReadOnlyList<(Guid Id, string Name)> _planes;

    public SelectPlaneDialog() : this([]) { }

    public SelectPlaneDialog(IReadOnlyList<(Guid Id, string Name)> planes)
    {
        _planes = planes;
        InitializeComponent();

        foreach (var (_, name) in _planes)
        {
            PlaneListBox.Items.Add(new ListBoxItem { Content = name });
        }

        PlaneListBox.SelectionChanged += OnSelectionChanged;
        KeyDown += OnKeyDown;

        // Pre-select Top if available
        var topIndex = _planes.Select((p, i) => (p, i))
            .FirstOrDefault(x => x.p.Name.Equals("Top", StringComparison.OrdinalIgnoreCase)).i;
        if (_planes.Count > 0)
            PlaneListBox.SelectedIndex = topIndex >= 0 ? topIndex : 0;
    }

    public Guid SelectedPlaneId { get; private set; }
    public string SelectedPlaneName { get; private set; } = string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = PlaneListBox.SelectedIndex >= 0;
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OkButton.IsEnabled)
        {
            Confirm();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (OkButton.IsEnabled)
            Confirm();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Confirm();

    private void Confirm()
    {
        var index = PlaneListBox.SelectedIndex;
        if (index < 0 || index >= _planes.Count)
            return;

        SelectedPlaneId = _planes[index].Id;
        SelectedPlaneName = _planes[index].Name;
        Close(new SelectPlaneDialogResult(SelectedPlaneId, SelectedPlaneName));
    }
}

public sealed record SelectPlaneDialogResult(Guid PlaneId, string PlaneName);
