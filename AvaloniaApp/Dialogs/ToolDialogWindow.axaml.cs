using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;
using Button = Avalonia.Controls.Button;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Reusable dialog scaffold for sketch / feature tools (Line, Extrude, Constraints, ...).
/// Hosts a tool-specific body control and emits a single bool result on close
/// (true = OK / confirmed, false = Cancel / dismissed).
/// </summary>
public sealed partial class ToolDialogWindow : AWindow
{
    private TextBlock TitleTextBlockControl => this.FindControl<TextBlock>("TitleTextBlock")
        ?? throw new InvalidOperationException("ToolDialogWindow is missing TitleTextBlock.");

    private TextBlock SubtitleTextBlockControl => this.FindControl<TextBlock>("SubtitleTextBlock")
        ?? throw new InvalidOperationException("ToolDialogWindow is missing SubtitleTextBlock.");

    private ContentControl BodyHostControl => this.FindControl<ContentControl>("BodyHost")
        ?? throw new InvalidOperationException("ToolDialogWindow is missing BodyHost.");

    private Button ConfirmButtonControl => this.FindControl<Button>("ConfirmButton")
        ?? throw new InvalidOperationException("ToolDialogWindow is missing ConfirmButton.");

    private Button CancelButtonControl => this.FindControl<Button>("CancelButton")
        ?? throw new InvalidOperationException("ToolDialogWindow is missing CancelButton.");

    public ToolDialogWindow()
    {
        InitializeComponent();
    }

    public ToolDialogWindow(string toolTitle, string subtitle, Avalonia.Controls.Control body)
        : this()
    {
        Title = toolTitle;
        TitleTextBlockControl.Text = toolTitle;
        SubtitleTextBlockControl.Text = subtitle;
        BodyHostControl.Content = body;
    }

    public Avalonia.Controls.Control? Body
    {
        get => BodyHostControl.Content as Avalonia.Controls.Control;
        set => BodyHostControl.Content = value;
    }

    public string ToolTitle
    {
        get => TitleTextBlockControl.Text ?? string.Empty;
        set
        {
            Title = value;
            TitleTextBlockControl.Text = value;
        }
    }

    public string Subtitle
    {
        get => SubtitleTextBlockControl.Text ?? string.Empty;
        set => SubtitleTextBlockControl.Text = value;
    }

    public string ConfirmButtonText
    {
        get => ConfirmButtonControl.Content?.ToString() ?? string.Empty;
        set => ConfirmButtonControl.Content = value;
    }

    public string CancelButtonText
    {
        get => CancelButtonControl.Content?.ToString() ?? string.Empty;
        set => CancelButtonControl.Content = value;
    }

    public bool ShowCancelButton
    {
        get => CancelButtonControl.IsVisible;
        set => CancelButtonControl.IsVisible = value;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}
