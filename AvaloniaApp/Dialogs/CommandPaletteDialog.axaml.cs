using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AWindow = Avalonia.Controls.Window;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Button = Avalonia.Controls.Button;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class CommandPaletteDialog : AWindow
{
    private readonly Func<string, CancellationToken, Task<string>> _executeAsync;
    private bool _isRunning;

    public CommandPaletteDialog()
        : this((_, _) => Task.FromResult("Command runner is not connected."))
    {
    }

    public CommandPaletteDialog(Func<string, CancellationToken, Task<string>> executeAsync)
    {
        _executeAsync = executeAsync;
        InitializeComponent();
        Opened += (_, _) => CommandInput.Focus();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnRunClick(object? sender, RoutedEventArgs e)
    {
        await RunCommandAsync();
        e.Handled = true;
    }

    private async void OnCommandInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            await RunCommandAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnQuickCommandClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string command)
        {
            CommandInput.Text = command;
            CommandInput.CaretIndex = command.Length;
            CommandInput.Focus();
            StatusTextBlock.Text = "Ready to run: " + command;
        }

        e.Handled = true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private async Task RunCommandAsync()
    {
        if (_isRunning)
        {
            return;
        }

        var command = CommandInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            StatusTextBlock.Text = "Type a CAD command first.";
            CommandInput.Focus();
            return;
        }

        _isRunning = true;
        RunButton.IsEnabled = false;
        StatusTextBlock.Text = "Running command sequence...";

        try
        {
            var result = await _executeAsync(command, CancellationToken.None);
            StatusTextBlock.Text = result;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Command failed: " + ex.Message;
        }
        finally
        {
            _isRunning = false;
            RunButton.IsEnabled = true;
            CommandInput.Focus();
        }
    }
}
