using Avalonia.Controls;
using FormaCore.Engine.Exact;
using My3DApp.AvaloniaApp.Services;

namespace My3DApp.AvaloniaApp.Controls;

/// <summary>Result of a legacy compatibility call on the native viewport.</summary>
public enum ViewportCompatResult
{
    Supported,
    Unsupported,
}

/// <summary>
/// The product viewport: native OCCT AIS/V3d inside the real My3DApp window.
///
/// New code must use <see cref="ExactHost"/> (ICadViewportHost — the exact-CAD
/// viewport contract). The WebViewportHost-shaped members below are a TRANSITIONAL
/// compatibility boundary for the legacy shell only:
///   - legacy events are declared but never raised (no fake interactions);
///   - unsupported legacy methods return <see cref="ViewportCompatResult.Unsupported"/>
///     and log to RuntimeLog — they never mutate state and never claim success;
///   - mesh-scene transport (SetSceneAsync) is deliberately ABSENT: mesh DTOs must
///     not reach the native viewport (see CAD_CANON).
/// Every compat call is counted in <see cref="CompatCallCounters"/> so remaining
/// call sites can be removed systematically.
/// </summary>
public sealed class StudioNativeViewport : Border
{
    private readonly OcctViewportControl _occt = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private OcctViewportHostAdapter? _host;

    public StudioNativeViewport()
    {
        Child = _occt;
        _occt.ViewerReady += (_, _) =>
        {
            _host = new OcctViewportHostAdapter(_occt);
            _ready.TrySetResult();
            ExactHostReady?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>The exact-CAD viewport contract. Null until the native viewer is created.</summary>
    public ICadViewportHost? ExactHost => _host;

    public event EventHandler? ExactHostReady;

    /// <summary>Per-member call counters for the transitional compatibility surface.</summary>
    public static readonly Dictionary<string, int> CompatCallCounters = new();

    // ---------------------------------------------------------------------
    // Legacy events — declared for source compatibility, never raised.
#pragma warning disable CS0067
    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ViewportSketchPlacementEventArgs>? SketchPlacementRequested;
    public event EventHandler<ViewportSketchPreviewEventArgs>? SketchPreviewRequested;
    public event EventHandler? SketchPreviewCleared;
    public event EventHandler? SketchStepCancelRequested;
    public event EventHandler? ToggleConstructionModeRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? MoveToolDeactivated;
    public event EventHandler<ViewportFaceClickedEventArgs>? FaceClicked;
    public event EventHandler<ViewportBoxSelectEventArgs>? BoxSelectCompleted;
    public event EventHandler? SelectionCleared;
    public event EventHandler<ViewportBodyTransformEventArgs>? BodyTransformCommitted;
    public event EventHandler? SketchTransformToolDeactivated;
    public event EventHandler<ViewportSketchEntityTransformEventArgs>? SketchEntityTransformCommitted;
    public event EventHandler? SketchRotateToolDeactivated;
    public event EventHandler<ViewportSketchEntityRotationEventArgs>? SketchEntityRotationCommitted;
    public event EventHandler<ViewportSketchEntityContextMenuEventArgs>? SketchEntityContextMenuRequested;
    public event EventHandler<ViewportMeasureResultEventArgs>? MeasureResultReceived;
    public event EventHandler? MeasureCancelled;
#pragma warning restore CS0067

    // ---------------------------------------------------------------------
    // Supported legacy calls (real native implementations).

    public Task EnsureInitializedAsync() => _ready.Task;

    public Task FocusSelectionAsync()
    {
        _host?.FitAll();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // Unsupported legacy calls. Explicit result, loud log, zero side effects.
    // NOTE: SetSceneAsync (mesh scene transport) is intentionally not defined.

    public Task<ViewportCompatResult> SetThemeAsync(StudioThemeMode mode) => Unsupported(nameof(SetThemeAsync));

    public Task<ViewportCompatResult> SetMoveToolActiveAsync(bool active) => Unsupported(nameof(SetMoveToolActiveAsync));

    public Task<ViewportCompatResult> SetSketchTransformToolActiveAsync(bool active) => Unsupported(nameof(SetSketchTransformToolActiveAsync));

    public Task<ViewportCompatResult> SetBoxSelectionAsync(IReadOnlyList<Guid> ids, bool append) => Unsupported(nameof(SetBoxSelectionAsync));

    public Task<ViewportCompatResult> SetSketchRotateToolActiveAsync(bool active) => Unsupported(nameof(SetSketchRotateToolActiveAsync));

    public Task<ViewportCompatResult> SetGridVisibleAsync(bool visible) => Unsupported(nameof(SetGridVisibleAsync));

    public Task<ViewportCompatResult> SetSectionPlaneAsync(bool enabled, string axis, double offset) => Unsupported(nameof(SetSectionPlaneAsync));

    public Task<ViewportCompatResult> SetMeasureModeAsync(bool active) => Unsupported(nameof(SetMeasureModeAsync));

    public Task<ViewportCompatResult> SetSketchToolHintAsync(string hint) => Unsupported(nameof(SetSketchToolHintAsync));

    private static Task<ViewportCompatResult> Unsupported(string member)
    {
        lock (CompatCallCounters)
        {
            CompatCallCounters[member] = CompatCallCounters.GetValueOrDefault(member) + 1;
        }

        RuntimeLog.Write("StudioNativeViewport",
            $"Legacy viewport call '{member}' is NOT supported by the native OCCT viewport; no action was taken.");
        return Task.FromResult(ViewportCompatResult.Unsupported);
    }
}
