namespace My3DApp.AvaloniaApp.Services;

/// <summary>
/// Transitional gate for the exact-kernel migration (single source of truth for
/// which legacy commands are hidden or suspended while the mesh pipeline is
/// replaced by ACL -> feature graph -> OCCT). Remove this class when the exact
/// vertical slice (sketch/extrude/hole/pattern on OCCT) replaces the legacy paths.
/// </summary>
public static class ExactMigration
{
    public const string SuspendedReason =
        "Being migrated to the exact B-Rep kernel (OCCT). Temporarily unavailable — no fake result is shown.";

    public const string HiddenReason =
        "Legacy concept removed from the product canon (ACL is the source of truth).";

    /// <summary>Commands whose only implementation was the old mesh viewport/model. Visible but disabled.</summary>
    public static readonly IReadOnlySet<string> SuspendedCommandIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "file.open", "file.recent", "file.save", "file.saveAs",
        "edit.undo", "edit.redo",
        "create.box", "create.cylinder", "create.sphere",
        "sketch.start", "sketch.finish", "sketch.cancel",
        "sketch.line", "sketch.rectangle", "sketch.circle", "sketch.arc", "sketch.point",
        "solid.extrude", "solid.revolve", "solid.sweep", "solid.loft",
        "solid.fillet", "solid.chamfer", "solid.hole", "solid.shell",
        "solid.linearPattern", "solid.circularPattern", "solid.mirror",
        "solid.booleanUnion", "solid.booleanSubtract", "solid.booleanIntersect",
        "transform.move", "transform.datumPlane",
        "view.measure", "view.section",
        "export.export", "export.quick",
    };

    /// <summary>Legacy product concepts contradicting the canon. Hidden entirely.</summary>
    public static readonly IReadOnlySet<string> HiddenCommandIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "create.templates",
        "recipe.openAcl", "recipe.openSample", "recipe.runActive",
        "export.prepare",
        "ai.openCopilot", "ai.configure",
    };

    /// <summary>Developer Mode gates kernel-validation commands (MY3DAPP_DEVELOPER=1).</summary>
    public static bool IsDeveloperMode =>
        Environment.GetEnvironmentVariable("MY3DAPP_DEVELOPER") == "1";
}
