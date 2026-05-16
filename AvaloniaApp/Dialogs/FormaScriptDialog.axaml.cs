using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using My3DApp.AvaloniaApp.Services;
using AWindow = Avalonia.Controls.Window;

namespace My3DApp.AvaloniaApp.Dialogs;

public sealed partial class FormaScriptDialog : AWindow
{
    private static string _lastScript = DefaultScript;

    private static readonly string DefaultScript =
        """
        // FormaScript — parametric CAD scripting
        // Variables support arithmetic: +  -  *  /  %  ^
        // Built-ins: sqrt() abs() sin() cos() tan() floor() ceil() round()  pi  e
        //
        // Blocks:
        //   repeat N  ...  end
        //   if expr   ...  end    (supports ==, !=, <, >, <=, >=)
        //
        // All CAD commands work (passed to the command engine):
        //   add box / add sphere / add cylinder ...
        //   start sketch on top / circle 0 0 25 / finish sketch
        //   extrude 20 / fillet 5 / shell 2 / mirror x
        //   linear pattern 3 spacing 50 along x
        //   circular pattern 6 angle 360 around y

        var r = 20
        var h = 60
        var n = 6

        add cylinder

        repeat n
          circular pattern n angle 360 around y
        end

        fillet 3
        """;

    private static readonly (string Name, string Code)[] Examples =
    [
        ("Parametric Box Grid", """
            var cols = 4
            var rows = 3
            var size = 20
            var gap = 5

            repeat rows
              repeat cols
                add box
                linear pattern cols spacing (size + gap) along x
              end
              linear pattern rows spacing (size + gap) along y
            end
            """),

        ("Hollow Cylinder", """
            var r = 30
            var h = 50
            var t = 4

            start sketch on top
              circle 0 0 r
            end sketch
            extrude h
            shell t
            """),

        ("Hex Bolt Head", """
            var flat = 13
            var r = flat / sqrt(3)
            var h = 8
            var shank_r = 5
            var shank_h = 25

            start sketch on top
              polygon 0 0 r 6
            end sketch
            extrude h

            start sketch on top
              circle 0 0 shank_r
            end sketch
            extrude shank_h

            boolean union
            fillet 1
            """),

        ("Gear Blank", """
            var teeth = 12
            var pitch_r = 30
            var hub_r = 10
            var thickness = 8

            start sketch on top
              circle 0 0 pitch_r
            end sketch
            extrude thickness
            circular pattern teeth angle 360 around y
            fillet 2
            """),

        ("Tower with Floors", """
            var floors = 5
            var floor_h = 15
            var width = 40
            var wall = 3

            repeat floors
              start sketch on top
                rectangle (-(width/2)) (-(width/2)) (width/2) (width/2)
              end sketch
              extrude floor_h
              shell wall
              linear pattern floors spacing floor_h along y
            end
            """),
    ];

    public FormaScriptDialog()
    {
        InitializeComponent();
        ScriptEditor.Text = _lastScript;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // Expose the parsed + executed commands.
    public FormaScriptResult? Result { get; private set; }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        var script = ScriptEditor.Text ?? string.Empty;
        _lastScript = script;

        var interpreter = new FormaScriptInterpreter();
        var result = interpreter.Execute(script);

        if (!result.IsSuccess)
        {
            StatusText.Text = $"Error: {result.ErrorMessage}";
            return;
        }

        if (result.Commands.Count == 0)
        {
            StatusText.Text = "Script produced no commands.";
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
        // Ctrl+Enter → run
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            OnRunClick(sender, new RoutedEventArgs());
        }
    }
}
