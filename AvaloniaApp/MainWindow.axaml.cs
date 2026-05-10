using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;

namespace My3DApp.AvaloniaApp;

public partial class MainWindow : Window
{
    private ViewportService? _viewport;

    public MainWindow()
    {
        InitializeComponent();
        Opened  += OnOpened;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Enter (without Shift) sends the AI prompt; Shift+Enter inserts a newline
        var inputBox = this.FindControl<TextBox>("AiInputBox");
        if (inputBox != null)
            inputBox.KeyDown += OnAiInputKeyDown;
    }

    private void OnAiInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter &&
            e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
        {
            if (DataContext is MainWindowViewModel vm && vm.AiSendCommand.CanExecute(null))
            {
                vm.AiSendCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Inject dialog service so VM can open file pickers without referencing Avalonia directly
        vm.FileDialogs = new FileDialogService(this);

        var host = this.FindControl<Border>("ViewportHost");
        if (host is null) return;

        try
        {
            _viewport = new ViewportService(host);
            await _viewport.InitializeAsync();
            vm.ViewportService = _viewport;
            vm.IsViewportLoading = false;
            RuntimeLog.Info("MainWindow", "Viewport initialized.");
        }
        catch (Exception ex)
        {
            RuntimeLog.Error("MainWindow", "Failed to initialize viewport.", ex);
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewport?.Dispose();
        (DataContext as IDisposable)?.Dispose();
        RuntimeLog.Info("MainWindow", "Window closing.");
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (path is null) continue;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".stl" or ".obj" or ".step" or ".stp" or ".3mf")) continue;

            await vm.ImportFileDirectAsync(path);
            break; // one file at a time
        }
    }

    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.KeyModifiers != Avalonia.Input.KeyModifiers.None) return; // let Ctrl+Z etc. go to menu

        switch (e.Key)
        {
            case Avalonia.Input.Key.S when vm.NewSketchCommand.CanExecute(null):
                vm.NewSketchCommand.Execute(null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.E when vm.ExtrudeCommand.CanExecute(null):
                vm.ExtrudeCommand.Execute(null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.R when vm.RevolveCommand.CanExecute(null):
                vm.RevolveCommand.Execute(null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.L when vm.LoftCommand.CanExecute(null):
                vm.LoftCommand.Execute(null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.H when vm.ShellCommand.CanExecute(null):
                vm.ShellCommand.Execute(null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.Escape:
                vm.StatusMessage = "Ready";
                e.Handled = true;
                break;
        }
    }
}
