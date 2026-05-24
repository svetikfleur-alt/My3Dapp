using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

/// <summary>
/// Reusable dialog scaffold for sketch / feature tools (Line, Extrude, Constraints, ...).
/// Hosts a tool-specific body control and emits a single bool result on close
/// (true = OK / confirmed, false = Cancel / dismissed).
/// </summary>
public sealed partial class ToolDialogWindow : AWindow
{
    public ToolDialogWindow()
    {
        InitializeComponent();
    }

    public ToolDialogWindow(string toolTitle, string subtitle, Avalonia.Controls.Control body)
        : this()
    {
        Title = toolTitle;
        TitleTextBlock.Text = toolTitle;
        SubtitleTextBlock.Text = subtitle;
        BodyHost.Content = body;
    }

    public Avalonia.Controls.Control? Body
    {
        get => BodyHost.Content as Avalonia.Controls.Control;
        set => BodyHost.Content = value;
    }

    public string ToolTitle
    {
        get => TitleTextBlock.Text ?? string.Empty;
        set
        {
            Title = value;
            TitleTextBlock.Text = value;
        }
    }

    public string Subtitle
    {
        get => SubtitleTextBlock.Text ?? string.Empty;
        set => SubtitleTextBlock.Text = value;
    }

    public string ConfirmButtonText
    {
        get => ConfirmButton.Content?.ToString() ?? string.Empty;
        set => ConfirmButton.Content = value;
    }

    public string CancelButtonText
    {
        get => CancelButton.Content?.ToString() ?? string.Empty;
        set => CancelButton.Content = value;
    }

    public bool ShowCancelButton
    {
        get => CancelButton.IsVisible;
        set => CancelButton.IsVisible = value;
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
