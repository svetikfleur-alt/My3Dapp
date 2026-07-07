using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FormaCore.Engine.Exact;
using My3DApp.AvaloniaApp.Controls;
using My3DApp.AvaloniaApp.Services;

namespace My3DApp.AvaloniaApp;

/// <summary>
/// P1 exact-spine window: Avalonia -> engine boundary -> OCCT B-Rep -> native AIS/V3d
/// viewport -> topology selection -> STEP export. Deliberately minimal; the product
/// Part Studio shell replaces this in P6.
/// </summary>
public sealed class ExactSpineWindow : Window
{
    private readonly OcctViewportControl _viewport = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4) };
    private readonly OcctExactKernel _kernel = new();

    private ICadViewportHost? _host;
    private IExactBodyHandle? _demoBody;

    public ExactSpineWindow()
    {
        Title = "My3DApp — Exact CAD Spine (P1)";
        Width = 1280;
        Height = 800;

        var toolbar = BuildToolbar();

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(toolbar);
        root.Children.Add(_status);
        root.Children.Add(_viewport);
        Content = root;

        _viewport.ViewerReady += (_, _) => Dispatcher.UIThread.Post(OnViewerReady);
        Closed += (_, _) =>
        {
            _demoBody?.Dispose();
            _kernel.Dispose();
        };

        _status.Text = "Initializing native OCCT viewport…";
    }

    private void OnViewerReady()
    {
        _host = new OcctViewportHostAdapter(_viewport);
        _host.SelectionChanged += (_, sel) => SetStatus($"Selected: {Describe(sel)}");
        _host.HoverChanged += (_, sel) => SetStatus(sel is null ? "Hover: —" : $"Hover: {Describe(sel)}");

        _host.SetSelectionMode(TopologyKind.Body, false);
        _host.SetSelectionMode(TopologyKind.Face, true);
        _host.SetSelectionMode(TopologyKind.Edge, true);

        CreateDemoModel();
        SetStatus("Ready — exact OCCT body displayed. RMB rotate · MMB pan · wheel zoom · LMB pick.");
    }

    private void CreateDemoModel()
    {
        // Exact P1 test model: 80x60x30 box minus a Ø24 through-cylinder.
        _demoBody?.Dispose();
        using var box = _kernel.CreateBox(80, 60, 30);
        using var hole = _kernel.CreateCylinder(12, 30);
        _demoBody = _kernel.BooleanSubtract(box, hole);
        _host!.DisplayBody(_demoBody);
        _host.SetView(CadStandardView.Isometric);
        _host.FitAll();
    }

    private Control BuildToolbar()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(6),
        };

        void Btn(string label, Action action)
        {
            var b = new Button { Content = label, Padding = new Thickness(10, 4) };
            b.Click += (_, _) =>
            {
                try { action(); }
                catch (Exception ex) { SetStatus($"ERROR: {ex.Message}"); }
            };
            bar.Children.Add(b);
        }

        Btn("Top", () => _host?.SetView(CadStandardView.Top));
        Btn("Front", () => _host?.SetView(CadStandardView.Front));
        Btn("Right", () => _host?.SetView(CadStandardView.Right));
        Btn("Iso", () => _host?.SetView(CadStandardView.Isometric));
        Btn("Fit", () => _host?.FitAll());
        bar.Children.Add(new Separator { Width = 12 });
        Btn("Shaded", () => _host?.SetDisplayMode(CadDisplayMode.Shaded));
        Btn("Shaded+Edges", () => _host?.SetDisplayMode(CadDisplayMode.ShadedWithEdges));
        Btn("Wireframe", () => _host?.SetDisplayMode(CadDisplayMode.Wireframe));
        bar.Children.Add(new Separator { Width = 12 });
        Btn("Sel: Body", () => ToggleSelection(TopologyKind.Body));
        Btn("Sel: Face", () => ToggleSelection(TopologyKind.Face));
        Btn("Sel: Edge", () => ToggleSelection(TopologyKind.Edge));
        Btn("Sel: Vertex", () => ToggleSelection(TopologyKind.Vertex));
        bar.Children.Add(new Separator { Width = 12 });
        Btn("Rebuild demo", CreateDemoModel);
        Btn("Export STEP…", () => _ = ExportStepAsync());
        return bar;
    }

    private readonly Dictionary<TopologyKind, bool> _selModes = new()
    {
        [TopologyKind.Body] = false,
        [TopologyKind.Face] = true,
        [TopologyKind.Edge] = true,
        [TopologyKind.Vertex] = false,
    };

    private void ToggleSelection(TopologyKind kind)
    {
        _selModes[kind] = !_selModes[kind];
        _host?.SetSelectionMode(kind, _selModes[kind]);
        SetStatus($"Selection modes: {string.Join(", ", _selModes.Where(kv => kv.Value).Select(kv => kv.Key))}");
    }

    private async Task ExportStepAsync()
    {
        if (_demoBody is null || _host is null)
        {
            SetStatus("No body to export.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export STEP",
            SuggestedFileName = "my3dapp-p1.step",
            FileTypeChoices = [new FilePickerFileType("STEP") { Patterns = ["*.step", "*.stp"] }],
        });
        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        ((IExactCadExporter)_kernel).ExportStep(_demoBody, path);
        var size = new FileInfo(path).Length;
        SetStatus($"STEP exported: {path} ({size:N0} bytes)");
    }

    private static string Describe(TopologySelection sel)
        => sel.Kind == TopologyKind.Body
            ? $"Body {sel.BodyId:N}"
            : $"{sel.Kind} #{sel.TransientIndex} of body {sel.BodyId:N} ({sel.Provenance})";

    private void SetStatus(string text) => Dispatcher.UIThread.Post(() => _status.Text = text);
}
