using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using My3DApp.AvaloniaApp.Services;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class SolidSculptDialog : AWindow
{
    private static string _lastScript = DefaultScript;

    private static readonly string DefaultScript =
        """
        // SolidSculpt — organic mesh sculpting DSL
        // Start with a base shape, then chain sculpt operations.
        //
        // base sphere [radius]           — spherical base
        // base box [w] [h] [d]           — rectangular base
        // base cylinder [radius] [height]
        //
        // subdivide [n]                  — loop subdivide (1–5 times)
        // smooth [iters] [strength]      — Laplacian smoothing
        // inflate [amount]               — push vertices outward along normal
        // noise [scale] [strength] [seed]— procedural noise displacement
        // pinch [strength] [falloff]     — pull toward centroid
        // twist [degrees]                — twist around Z
        // bend [degrees]                 — bend along X
        // taper [topScale] [botScale]    — taper by height
        // spherize [strength] [radius]   — blend toward sphere

        base sphere 30
        subdivide 2
        smooth 4 0.5
        noise 0.12 6 17
        smooth 2 0.3
        """;

    private static readonly (string Name, string Code)[] Examples =
    [
        ("Rocky Asteroid", """
            base sphere 40
            subdivide 2
            noise 0.08 12 31
            smooth 3 0.4
            noise 0.25 4 99
            smooth 1 0.2
            """),

        ("Organic Blob", """
            base sphere 30
            subdivide 3
            smooth 2 0.6
            inflate 5
            noise 0.1 8 42
            spherize 0.4
            smooth 3 0.5
            """),

        ("Twisted Pillar", """
            base cylinder 15 80
            subdivide 2
            smooth 2 0.5
            twist 120
            noise 0.05 3 7
            """),

        ("Tapered Crystal", """
            base cylinder 20 60
            subdivide 2
            taper 0.1 1.0
            noise 0.15 5 13
            smooth 2 0.4
            twist 30
            """),

        ("Organic Shell", """
            base sphere 35
            subdivide 2
            taper 0.2 1.0
            bend 45
            smooth 3 0.5
            noise 0.08 4 55
            """),

        ("Bumpy Torus (approx)", """
            base sphere 30
            subdivide 2
            smooth 4 0.7
            pinch 0.6 25
            noise 0.2 5 77
            smooth 2 0.3
            """),
    ];

    public SolidSculptDialog()
    {
        InitializeComponent();
        ScriptEditor.Text = _lastScript;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public SolidSculptResult? Result { get; private set; }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        var script = ScriptEditor.Text ?? string.Empty;
        _lastScript = script;

        var interpreter = new SolidSculptInterpreter();
        var result = interpreter.Execute(script);

        if (!result.IsSuccess)
        {
            StatusText.Text = $"Error: {result.ErrorMessage}";
            return;
        }

        Result = result;
        StatusText.Text = string.Empty;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        ScriptEditor.Text = string.Empty;
        StatusText.Text = string.Empty;
    }

    private void OnExamplesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var menu = new ContextMenu();
        foreach (var (name, code) in Examples)
        {
            var item = new MenuItem { Header = name };
            var capturedCode = code;
            item.Click += (_, _) =>
            {
                ScriptEditor.Text = capturedCode;
                StatusText.Text = string.Empty;
            };
            menu.Items.Add(item);
        }
        menu.Open(btn);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            OnApplyClick(sender, new RoutedEventArgs());
        }
    }
}
