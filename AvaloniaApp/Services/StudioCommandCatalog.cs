namespace My3DApp.AvaloniaApp.Services;

public sealed record StudioCommandDefinition(
    string Id,
    string Label,
    string Group,
    string Location,
    string Handler,
    bool IsMvpRelevant,
    string? IconKey = null,
    bool IsExperimental = false,
    string? Notes = null);

public sealed record StudioCommandState(
    StudioCommandDefinition Definition,
    bool IsVisible,
    bool IsEnabled,
    string Tooltip,
    string? DisabledReason = null)
{
    public string Status =>
        !IsVisible
            ? "Hidden"
            : Definition.IsExperimental
                ? "Experimental"
                : IsEnabled
                    ? "Working"
                    : "Disabled";
}

public static class StudioCommandCatalog
{
    public static IReadOnlyList<StudioCommandDefinition> All { get; } =
    [
        new("file.new", "New Project", "File", "Menu + Toolbar", "OnNewProjectClick", true, "plus.svg"),
        new("file.open", "Open Project", "File", "Menu + Toolbar", "OnOpenProjectClick", true, "open.svg"),
        new("file.recent", "Recent Projects", "File", "Menu + Toolbar", "OnRecentFilesClick", true, "chevron-down.svg"),
        new("file.save", "Save Project", "File", "Menu + Toolbar", "OnSaveProjectClick", true, "save.svg"),
        new("file.saveAs", "Save Project As", "File", "Menu + Toolbar", "OnSaveAsProjectClick", true),
        new("edit.undo", "Undo", "File", "Toolbar", "OnUndoClick", true, "undo.svg"),
        new("edit.redo", "Redo", "File", "Toolbar", "OnRedoClick", true, "redo.svg"),
        new("create.box", "Box", "Create", "Menu + Toolbar", "OnPrimitiveMenuItemClick", true, "box.svg"),
        new("create.cylinder", "Cylinder", "Create", "Menu", "OnPrimitiveMenuItemClick", true, "cylinder.svg"),
        new("create.sphere", "Sphere", "Create", "Menu", "OnPrimitiveMenuItemClick", false, "sphere.svg"),
        new("create.templates", "Open Templates", "Create", "Toolbar", "OnOpenTemplatesWorkspaceClick", true),
        new("sketch.start", "Start Sketch", "Sketch", "Menu + Toolbar", "OnSketchPrimaryClick", true, "pencil.svg"),
        new("sketch.finish", "Finish Sketch", "Sketch", "Menu + Toolbar", "OnSketchPrimaryClick", true, "check.svg"),
        new("sketch.cancel", "Cancel Sketch", "Sketch", "Menu + Toolbar", "OnSketchCancelClick", true, "delete.svg"),
        new("sketch.line", "Line", "Sketch", "Menu + Toolbar", "OnSketchToolClick", true, "line.svg"),
        new("sketch.rectangle", "Rectangle", "Sketch", "Menu + Toolbar", "OnSketchToolClick", true, "rectangle.svg"),
        new("sketch.circle", "Circle", "Sketch", "Menu + Toolbar", "OnSketchToolClick", true, "circle.svg"),
        new("sketch.arc", "Arc", "Sketch", "Menu + Toolbar", "OnSketchToolClick", false, "arc.svg"),
        new("sketch.point", "Point", "Sketch", "Menu + Toolbar", "OnSketchToolClick", false, "point.svg"),
        new("solid.extrude", "Extrude", "Solid", "Menu + Toolbar", "OnExtrudeClick", true, "extrude.svg"),
        new("solid.revolve", "Revolve", "Solid", "Experimental", "OnRevolveClick", false, "revolve.svg", true),
        new("solid.sweep", "Sweep", "Solid", "Experimental", "OnSweepClick", false, "extrude.svg", true),
        new("solid.loft", "Loft", "Solid", "Experimental", "OnLoftClick", false, "revolve.svg", true),
        new("transform.move", "Move Tool", "Transform", "Toolbar + Menu", "OnMoveToolClick", false, "move.svg"),
        new("transform.datumPlane", "Create Datum Plane", "Transform", "Experimental", "OnCreateDatumPlaneClick", false, "sketch-plane.svg", true),
        new("view.fit", "Fit View", "View", "Menu + Toolbar", "OnFocusSelectionClick", true, "focus.svg"),
        new("view.measure", "Measure", "View", "Toolbar", "OnMeasureToolToggleClick", false, null, true),
        new("view.section", "Section View", "View", "Toolbar", "OnSectionViewToggleClick", false, null, true),
        new("view.commandPalette", "Command Palette", "View", "Menu + Top Bar", "OnCommandPaletteClick", true),
        new("ai.openCopilot", "Open Copilot", "AI", "Menu + Toolbar", "OnOpenAssistantWorkspaceClick", true),
        new("ai.configure", "Configure AI", "AI", "Menu + Toolbar", "OnAiSettingsClick", true),
        new("recipe.openAcl", "Open ACL Panel", "Recipe", "Menu + Toolbar", "OnOpenAclPanelClick", true),
        new("recipe.openSample", "Open Sample Recipe", "Recipe", "Menu + Toolbar", "OnOpenSampleRecipeClick", true),
        new("recipe.runActive", "Run Active Recipe", "Recipe", "Menu + Toolbar", "OnRunActiveRecipeClick", true),
        new("export.prepare", "Prepare Workspace", "Export", "Menu + Toolbar", "OnPrepareWorkspaceMenuClick", true),
        new("export.export", "Export STL / OBJ", "Export", "Menu + Toolbar", "OnExportClick", true, "export.svg"),
        new("export.quick", "Quick Export STL", "Export", "Experimental", "OnQuickExportClick", false, "export.svg", true),
        new("experimental.fillet", "Fillet", "Experimental", "Menu", "OnFilletClick", false, "fillet.svg", true),
        new("experimental.chamfer", "Chamfer", "Experimental", "Menu", "OnChamferClick", false, "chamfer.svg", true),
        new("experimental.hole", "Hole", "Experimental", "Menu", "OnHoleClick", false, "hole.svg", true),
        new("experimental.shell", "Shell", "Experimental", "Menu", "OnShellClick", false, "shell.svg", true),
        new("experimental.linearPattern", "Linear Pattern", "Experimental", "Menu", "OnLinearPatternClick", false, "linear-pattern.svg", true),
        new("experimental.circularPattern", "Circular Pattern", "Experimental", "Menu", "OnCircularPatternClick", false, "circular-pattern.svg", true),
        new("experimental.mirror", "Mirror Body", "Experimental", "Menu", "OnMirrorMenuItemClick", false, "mirror.svg", true),
        new("experimental.booleanUnion", "Boolean Union", "Experimental", "Menu", "OnBooleanUnionClick", false, "boolean-union.svg", true),
        new("experimental.booleanSubtract", "Boolean Subtract", "Experimental", "Menu", "OnBooleanSubtractClick", false, "boolean-subtract.svg", true),
        new("experimental.booleanIntersect", "Boolean Intersect", "Experimental", "Menu", "OnBooleanIntersectClick", false, "boolean-intersect.svg", true)
    ];

    public static StudioCommandDefinition GetRequired(string id) =>
        All.FirstOrDefault(command => string.Equals(command.Id, id, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Unknown studio command id '{id}'.");
}
