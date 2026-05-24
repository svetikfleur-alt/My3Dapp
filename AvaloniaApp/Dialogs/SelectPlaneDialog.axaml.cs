using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;
using Button = Avalonia.Controls.Button;
using ListBox = Avalonia.Controls.ListBox;
using ListBoxItem = Avalonia.Controls.ListBoxItem;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class SelectPlaneDialog : AWindow
{
    private readonly IReadOnlyList<(Guid Id, string Name)> _planes;
    private ListBox PlaneListBoxControl => this.FindControl<ListBox>("PlaneListBox")
        ?? throw new InvalidOperationException("SelectPlaneDialog is missing PlaneListBox.");

    private Button OkButtonControl => this.FindControl<Button>("OkButton")
        ?? throw new InvalidOperationException("SelectPlaneDialog is missing OkButton.");

    public SelectPlaneDialog() : this([]) { }

    public SelectPlaneDialog(IReadOnlyList<(Guid Id, string Name)> planes)
    {
        _planes = planes;
        InitializeComponent();

        foreach (var (_, name) in _planes)
        {
            PlaneListBoxControl.Items.Add(new ListBoxItem { Content = name });
        }

        PlaneListBoxControl.SelectionChanged += OnSelectionChanged;
        KeyDown += OnKeyDown;

        // Pre-select Top if available
        var topIndex = _planes.Select((p, i) => (p, i))
            .FirstOrDefault(x => x.p.Name.Equals("Top", StringComparison.OrdinalIgnoreCase)).i;
        if (_planes.Count > 0)
            PlaneListBoxControl.SelectedIndex = topIndex >= 0 ? topIndex : 0;
    }

    public Guid SelectedPlaneId { get; private set; }
    public string SelectedPlaneName { get; private set; } = string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        OkButtonControl.IsEnabled = PlaneListBoxControl.SelectedIndex >= 0;
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OkButtonControl.IsEnabled)
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
        if (OkButtonControl.IsEnabled)
            Confirm();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Confirm();

    private void Confirm()
    {
        var index = PlaneListBoxControl.SelectedIndex;
        if (index < 0 || index >= _planes.Count)
            return;

        SelectedPlaneId = _planes[index].Id;
        SelectedPlaneName = _planes[index].Name;
        Close(new SelectPlaneDialogResult(SelectedPlaneId, SelectedPlaneName));
    }
}

public sealed record SelectPlaneDialogResult(Guid PlaneId, string PlaneName);
