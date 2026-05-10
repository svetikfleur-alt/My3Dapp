using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FormaCore.Core;
using FormaCore.Engine;
using FormaCore.Export;
using My3DApp.AvaloniaApp.Controls;

namespace My3DApp.AvaloniaApp.Services;

public sealed class StudioWorkspaceController
{
    private static readonly JsonSerializerOptions ProjectJsonOptions = new()
    {
        WriteIndented = true,
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private CadProjectStore _store = new();
    private readonly SolidMesher _mesher = new();
    private readonly List<string> _actionLog = [];
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private const int MaxUndoDepth = 50;
    private int _mutationCount;
    private int _savedMutationCount;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasUnsavedChanges => _mutationCount != _savedMutationCount;

    public StudioWorkspaceController()
    {
        CurrentState = BuildState("Studio ready.");
        AppendActionLog(CurrentState.StatusMessage);
        CurrentState = BuildState(CurrentState.StatusMessage);
    }

    public StudioWorkspaceState CurrentState { get; private set; }

    public event EventHandler<StudioWorkspaceState>? WorkspaceChanged;

    private const int CurrentSchemaVersion = 1;

    public void SaveProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Project path is missing.", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var envelope = new ProjectFileEnvelope
        {
            SchemaVersion = CurrentSchemaVersion,
            Project = _store.Project
        };
        var json = JsonSerializer.Serialize(envelope, ProjectJsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
        _savedMutationCount = _mutationCount;
        PublishSuccess($"Saved project to {Path.GetFileName(path)}.", mutated: false);
    }

    public StudioWorkspaceActionResult OpenProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PublishFailure("Project path is missing.");
        }

        if (!File.Exists(path))
        {
            return PublishFailure($"Project file not found: {path}");
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);

            // Try versioned envelope first; fall back to raw CadProject for backwards compat.
            CadProject? project = null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("schemaVersion", out var versionEl))
            {
                var version = versionEl.GetInt32();
                if (version != CurrentSchemaVersion)
                {
                    return PublishFailure(
                        $"Project was saved with schema version {version}, but this build expects version {CurrentSchemaVersion}. Please update the app.");
                }
                var envelope = JsonSerializer.Deserialize<ProjectFileEnvelope>(json, ProjectJsonOptions);
                project = envelope?.Project;
            }
            else
            {
                project = JsonSerializer.Deserialize<CadProject>(json, ProjectJsonOptions);
            }

            if (project is null)
            {
                return PublishFailure("Project file was empty or invalid.");
            }

            NormalizeProjectForLoad(project);
            _store = new CadProjectStore(project);
            _savedMutationCount = _mutationCount;
            return PublishSuccess($"Opened project {Path.GetFileName(path)}.", mutated: false);
        }
        catch (JsonException ex)
        {
            return PublishFailure($"Project file is not valid: {ex.Message}");
        }
        catch (IOException ex)
        {
            return PublishFailure($"Project file could not be read: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return PublishFailure($"Project file is not accessible: {ex.Message}");
        }
    }

    private sealed class ProjectFileEnvelope
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public CadProject? Project { get; set; }
    }

    private static void NormalizeProjectForLoad(CadProject project)
    {
        project.Name = string.IsNullOrWhiteSpace(project.Name) ? "My3DApp Project" : project.Name.Trim();
        project.Units = string.IsNullOrWhiteSpace(project.Units) ? "mm" : project.Units.Trim();
        project.Scene ??= new CadScene();
        project.Scene.Bodies ??= [];
        project.Scene.ReferencePlanes ??= [];
        project.Scene.EnsureReferencePlanes();
        project.ActiveSketchSession = null;

        project.Scene.Bodies.RemoveAll(body => body is null);
        foreach (var body in project.Scene.Bodies)
        {
            body.Name = string.IsNullOrWhiteSpace(body.Name) ? "Body" : body.Name.Trim();
            body.Features ??= [];
            body.Features.RemoveAll(feature => feature is null);

            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                sketch.PlaneName = string.IsNullOrWhiteSpace(sketch.PlaneName) ? "Top" : sketch.PlaneName.Trim();
                sketch.Entities ??= [];
                sketch.Constraints ??= [];
                sketch.Dimensions ??= [];
                sketch.Entities.RemoveAll(entity => entity is null);
            }
        }

        if (!SelectionExists(project, project.Selection))
        {
            project.Selection = CadSelection.None;
        }
    }

    private static bool SelectionExists(CadProject project, CadSelection selection)
    {
        if (selection.IsEmpty)
        {
            return true;
        }

        return selection.Kind switch
        {
            CadEntityKind.ReferencePlane => project.Scene.ReferencePlanes.Any(plane => plane.Id == selection.EntityId),
            CadEntityKind.Body => project.Scene.Bodies.Any(body => body.Id == selection.EntityId),
            CadEntityKind.Feature or CadEntityKind.Sketch => project.Scene.Bodies
                .SelectMany(body => body.Features)
                .Any(feature => feature.Id == selection.EntityId),
            CadEntityKind.SketchEntity => project.Scene.Bodies
                .SelectMany(body => body.Features.OfType<SketchFeature>())
                .SelectMany(sketch => sketch.Entities)
                .Any(entity => entity.Id == selection.EntityId),
            _ => false
        };
    }

    public StudioWorkspaceActionResult Undo()
    {
        if (_undoStack.Count == 0)
        {
            return PublishFailure("Nothing to undo.");
        }

        var redoJson = JsonSerializer.Serialize(_store.Project, ProjectJsonOptions);
        _redoStack.Push(redoJson);

        var undoJson = _undoStack.Pop();
        RestoreFromJson(undoJson);
        return PublishSuccess("Undo.", mutated: true);
    }

    public StudioWorkspaceActionResult Redo()
    {
        if (_redoStack.Count == 0)
        {
            return PublishFailure("Nothing to redo.");
        }

        var undoJson = JsonSerializer.Serialize(_store.Project, ProjectJsonOptions);
        _undoStack.Push(undoJson);

        var redoJson = _redoStack.Pop();
        RestoreFromJson(redoJson);
        return PublishSuccess("Redo.", mutated: true);
    }

    private void PushUndoSnapshot()
    {
        _mutationCount++;
        var json = JsonSerializer.Serialize(_store.Project, ProjectJsonOptions);
        _undoStack.Push(json);
        if (_undoStack.Count > MaxUndoDepth)
        {
            var trimmed = _undoStack.ToArray();
            _undoStack.Clear();
            foreach (var item in trimmed.Take(MaxUndoDepth).Reverse())
            {
                _undoStack.Push(item);
            }
        }

        _redoStack.Clear();
    }

    private void RestoreFromJson(string json)
    {
        var project = JsonSerializer.Deserialize<CadProject>(json, ProjectJsonOptions);
        if (project is null)
        {
            return;
        }

        NormalizeProjectForLoad(project);
        _store = new CadProjectStore(project);
    }

    public StudioWorkspaceActionResult ExecuteCommand(CadViewportCommand command)
    {
        if (command is null)
        {
            return PublishFailure("No CAD command was provided.");
        }

        if (command.Kind == CadViewportCommandKind.FocusCamera)
        {
            return PublishSuccess("Focused current selection.", mutated: false, requestFocusSelection: true);
        }

        var action = TranslateCommand(command);
        if (action is null)
        {
            return PublishFailure($"Unsupported command: {command.Describe()}");
        }

        var isMutatingAction = action.Kind is not (CadCommandActionKind.UpdateSketchPreview
            or CadCommandActionKind.ClearSketchPreview
            or CadCommandActionKind.CancelSketchStep
            or CadCommandActionKind.SelectEntity
            or CadCommandActionKind.SelectPlane
            or CadCommandActionKind.FocusSelection);

        if (isMutatingAction)
        {
            PushUndoSnapshot();
        }

        var result = _store.Apply(action);
        if (action.Kind is CadCommandActionKind.UpdateSketchPreview
            or CadCommandActionKind.ClearSketchPreview
            or CadCommandActionKind.CancelSketchStep)
        {
            return PublishCadResultQuietly(result);
        }

        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult SelectEntity(Guid entityId)
    {
        if (entityId == Guid.Empty)
        {
            return PublishFailure("Selection requires a valid entity.");
        }

        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.SelectEntity,
            EntityId: entityId));

        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult DeleteSelection()
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(CadCommandActionKind.DeleteSelection));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult ApplySelectionParameter(string key, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return PublishFailure("Parameter name is missing.");
        }

        if (!TryParseNumber(rawValue, out var value))
        {
            return PublishFailure($"Value '{rawValue}' is not a valid number.");
        }

        PushUndoSnapshot();
        var selection = _store.Project.Selection;
        if (selection.IsEmpty)
        {
            return PublishFailure("Select a feature or part before editing parameters.");
        }

        if (selection.Kind == CadEntityKind.Body)
        {
            var body = _store.Project.Scene.Bodies.FirstOrDefault(item => item.Id == selection.EntityId);
            if (body is null)
            {
                return PublishFailure("Selected part no longer exists.");
            }

            if (TryApplyBodyParameter(body, key, value))
            {
                return PublishSuccess($"Updated {key}.", mutated: true);
            }

            return PublishFailure($"Parameter {key} is not editable for {body.Name}.");
        }

        if (selection.Kind == CadEntityKind.SketchEntity)
        {
            var sketchEntity = FindSelectedSketchEntity(selection.EntityId);
            if (sketchEntity is null)
            {
                return PublishFailure("Selected sketch entity no longer exists.");
            }

            if (!sketchEntity.TrySetParameter(key, value))
            {
                return PublishFailure($"Parameter {key} is not editable for {sketchEntity.EntityType}.");
            }

            return PublishSuccess($"Updated {sketchEntity.EntityType} {key}.", mutated: true);
        }

        var feature = FindSelectedFeature(selection.EntityId);
        if (feature is null)
        {
            return PublishFailure("Selected feature no longer exists.");
        }

        if (!feature.TrySetParameter(key, value))
        {
            return PublishFailure($"Parameter {key} is not editable for {feature.Name}.");
        }

        return PublishSuccess($"Updated {feature.Name} {key}.", mutated: true);
    }

    public StudioWorkspaceActionResult ApplyBodyTranslation(Guid bodyId, double x, double y, double z)
    {
        var body = _store.Project.Scene.Bodies.FirstOrDefault(item => item.Id == bodyId);
        if (body is null)
        {
            return PublishFailure("Dragged part no longer exists.");
        }

        SetBodyTranslation(body, x, y, z);
        _store.Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);
        return PublishSuccess($"Moved {body.Name}.", mutated: true);
    }

    public StudioWorkspaceActionResult ApplySketchEntityTranslation(Guid sketchFeatureId, double du, double dv)
    {
        SketchFeature? target = null;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var sketch = body.Features.OfType<SketchFeature>().FirstOrDefault(s => s.Id == sketchFeatureId);
            if (sketch is not null)
            {
                target = sketch;
                break;
            }
        }

        if (target is null)
        {
            return PublishFailure("Sketch feature not found for transform.");
        }

        TranslateSketchEntities(target.Entities, du, dv);
        return PublishSuccess($"Translated sketch entities by ({du:0.###}, {dv:0.###}).", mutated: true);
    }

    public StudioWorkspaceActionResult ApplySketchEntityRotation(Guid sketchFeatureId, double pivotU, double pivotV, double angleDeg)
    {
        SketchFeature? target = null;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var sketch = body.Features.OfType<SketchFeature>().FirstOrDefault(s => s.Id == sketchFeatureId);
            if (sketch is not null) { target = sketch; break; }
        }

        if (target is null)
            return PublishFailure("Sketch feature not found for rotate.");

        RotateSketchEntities(target.Entities, pivotU, pivotV, angleDeg);
        return PublishSuccess($"Rotated sketch entities by {angleDeg:0.#}°.", mutated: true);
    }

    public StudioWorkspaceActionResult Refresh()
    {
        var state = BuildState("Workspace refreshed.");
        PublishState(state);
        return new StudioWorkspaceActionResult(true, false, state.StatusMessage, false);
    }

    public void SelectReferencePlane(Guid planeId, string planeName)
    {
        _store.Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, planeId, planeName);
        var state = BuildState($"Plane '{planeName}' selected.");
        PublishState(state);
    }

    public void ClearSelection()
    {
        _store.Project.Selection = CadSelection.None;
        _store.Project.SelectedFacePlane = null;
        var state = BuildState("Selection cleared.");
        PublishState(state);
    }

    public void SetSelectedFacePlane(CadFacePlane? facePlane)
    {
        _store.Project.SelectedFacePlane = facePlane;
        var state = BuildState(facePlane is null ? "Face deselected." : "Face selected — click Start Sketch to sketch on this face.");
        PublishState(state);
    }

    public ViewportRenderBody? ComputeExtrudePreview(double distance, bool reverseDirection)
    {
        try
        {
            if (!_store.TryComputeExtrudePreviewSolid(distance, reverseDirection, out var solid, out _))
                return null;

            var mesh = _mesher.Tessellate(solid);
            return new ViewportRenderBody
            {
                BodyId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Preview",
                Kind = "Preview",
                X = 0, Y = 0, Z = 0,
                Positions = BuildLocalPositions(mesh, new Vector3D(0, 0, 0)),
                Indices = BuildIndices(mesh),
                FeatureIds = []
            };
        }
        catch
        {
            return null;
        }
    }

    private StudioWorkspaceActionResult PublishCadResult(CadActionResult result)
    {
        if (result.IsSuccess)
        {
            return PublishSuccess(result.Message, result.Mutated, false, result.Snapshot);
        }

        return PublishFailure(result.Message, result.Snapshot);
    }

    private StudioWorkspaceActionResult PublishCadResultQuietly(CadActionResult result)
    {
        var state = BuildState(CurrentState.StatusMessage, result.Snapshot);
        PublishState(state);
        return new StudioWorkspaceActionResult(result.IsSuccess, result.Mutated, result.Message, false);
    }

    private StudioWorkspaceActionResult PublishSuccess(
        string message,
        bool mutated,
        bool requestFocusSelection = false,
        CadCompileResult? snapshot = null)
    {
        AppendActionLog(message);
        var state = BuildState(message, snapshot);
        PublishState(state);
        return new StudioWorkspaceActionResult(true, mutated, message, requestFocusSelection);
    }

    private StudioWorkspaceActionResult PublishFailure(string message, CadCompileResult? snapshot = null)
    {
        AppendActionLog($"FAILED: {message}");
        var state = BuildState(message, snapshot);
        PublishState(state);
        return new StudioWorkspaceActionResult(false, false, message, false);
    }

    private void PublishState(StudioWorkspaceState state)
    {
        CurrentState = state;
        WorkspaceChanged?.Invoke(this, state);
    }

    private StudioWorkspaceState BuildState(string statusMessage, CadCompileResult? snapshot = null)
    {
        var compileResult = snapshot ?? _store.Compile();
        return new StudioWorkspaceState(
            _store.Project,
            compileResult,
            BuildViewportState(compileResult),
            CadProjectTextExporter.Export(_store.Project),
            BuildLogText(compileResult),
            statusMessage);
    }

    private ViewportRenderState BuildViewportState(CadCompileResult compileResult)
    {
        var selectedBodyId = ResolveSelectedBodyId();
        var selectedPlaneId = ResolveSelectedPlaneId();
        var selectedSketchId = ResolveSelectedSketchId();
        var bodyLookup = _store.Project.Scene.Bodies.ToDictionary(body => body.Id);
        var renderBodies = new List<ViewportRenderBody>();
        var renderSketches = BuildRenderSketches(_store.Project);

        // During sketch edit, suppress bodies whose features reference the edited sketch.
        var editingSketchId = _store.Project.ActiveSketchSession?.EditingSketchId ?? Guid.Empty;
        var suppressedBodyIds = editingSketchId != Guid.Empty
            ? _store.Project.Scene.Bodies
                .Where(b => b.Features.Any(f =>
                    (f is ExtrudeFeature ex && ex.SketchFeatureId == editingSketchId) ||
                    (f is RevolveFeature rv && rv.SketchFeatureId == editingSketchId)))
                .Select(b => b.Id)
                .ToHashSet()
            : null;

        foreach (var compiledBody in compileResult.Bodies)
        {
            if (!bodyLookup.TryGetValue(compiledBody.BodyId, out var sourceBody))
            {
                continue;
            }

            if (!sourceBody.Visible)
            {
                continue;
            }

            if (suppressedBodyIds is not null && suppressedBodyIds.Contains(compiledBody.BodyId))
            {
                continue;
            }

            try
            {
                var mesh = _mesher.Tessellate(compiledBody.Solid);
                var translation = GetBodyTranslation(sourceBody);
                renderBodies.Add(new ViewportRenderBody
                {
                    BodyId = compiledBody.BodyId,
                    Name = compiledBody.Name,
                    Kind = compiledBody.SourceKind.ToString(),
                    X = translation.X,
                    Y = translation.Y,
                    Z = translation.Z,
                    Positions = BuildLocalPositions(mesh, translation),
                    Indices = BuildIndices(mesh),
                    FeatureIds = compiledBody.FeatureIds.ToArray(),
                    Color = sourceBody.Color
                });
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("Workspace", $"Failed to build display mesh for {compiledBody.Name}.", ex);
            }
        }

        var activeFp = _store.Project.ActiveSketchSession?.FacePlane;
        var activeFacePlane = activeFp is null ? null : new ViewportActiveFacePlane
        {
            Nx = activeFp.NormalX, Ny = activeFp.NormalY, Nz = activeFp.NormalZ,
            Ox = activeFp.OriginX, Oy = activeFp.OriginY, Oz = activeFp.OriginZ,
            Ux = activeFp.UAxisX, Uy = activeFp.UAxisY, Uz = activeFp.UAxisZ,
            Vx = activeFp.VAxisX, Vy = activeFp.VAxisY, Vz = activeFp.VAxisZ
        };

        return new ViewportRenderState
        {
            Mode = compileResult.Mode.ToString(),
            ActivePlaneName = compileResult.ActivePlaneName,
            ActiveFacePlane = activeFacePlane,
            SelectedBodyId = selectedBodyId,
            SelectedPlaneId = selectedPlaneId,
            SelectedSketchId = selectedSketchId,
            Planes = compileResult.Planes
                .Select(plane => new ViewportRenderPlane
                {
                    PlaneId = plane.PlaneId,
                    Name = plane.Name,
                    Kind = plane.Kind.ToString(),
                    Visible = plane.Visible
                })
                .ToList(),
            Sketches = renderSketches,
            Bodies = renderBodies
        };
    }

    private string BuildLogText(CadCompileResult compileResult)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Action Log");
        builder.AppendLine(new string('=', 80));
        foreach (var entry in _actionLog.TakeLast(40))
        {
            builder.AppendLine(entry);
        }

        if (compileResult.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Compiler Diagnostics");
            builder.AppendLine(new string('=', 80));
            foreach (var diagnostic in compileResult.Diagnostics)
            {
                builder.AppendLine($"[{diagnostic.Severity}] {diagnostic.Message}");
            }
        }

        var runtimeLogPath = Path.Combine(AppContext.BaseDirectory, "logs", "runtime-errors.log");
        if (File.Exists(runtimeLogPath))
        {
            builder.AppendLine();
            builder.AppendLine("Runtime Log");
            builder.AppendLine(new string('=', 80));
            builder.AppendLine(File.ReadAllText(runtimeLogPath));
        }

        return builder.ToString().TrimEnd();
    }

    public CadAssistantContext GetAssistantContext(
        string appMode,
        string? activeTool,
        string? selectedPlane,
        string assistantMode,
        IReadOnlyList<string> selectedFeatures)
    {
        var project = _store.Project;
        var session = project.ActiveSketchSession;
        return new CadAssistantContext(
            Mode: assistantMode,
            AppMode: appMode,
            ActiveTool: activeTool,
            SelectedPlane: selectedPlane,
            SelectedFeatures: selectedFeatures,
            RecentActions: _actionLog.TakeLast(10).ToArray(),
            SketchEntityCount: session?.DraftEntities.Count ?? 0,
            BodyCount: project.Scene.Bodies.Count(b => b.Visible));
    }

    private void AppendActionLog(string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        _actionLog.Add($"[{timestamp}] {message}");
    }

    private static double[] BuildLocalPositions(Mesh mesh, Vector3D translation)
    {
        var positions = new double[mesh.Vertices.Count * 3];
        for (var index = 0; index < mesh.Vertices.Count; index++)
        {
            var vertex = mesh.Vertices[index];
            var target = index * 3;
            positions[target] = vertex.X - translation.X;
            positions[target + 1] = vertex.Y - translation.Y;
            positions[target + 2] = vertex.Z - translation.Z;
        }

        return positions;
    }

    private static int[] BuildIndices(Mesh mesh)
    {
        var indices = new int[mesh.Triangles.Count * 3];
        for (var index = 0; index < mesh.Triangles.Count; index++)
        {
            var triangle = mesh.Triangles[index];
            var target = index * 3;
            indices[target] = triangle.A;
            indices[target + 1] = triangle.B;
            indices[target + 2] = triangle.C;
        }

        return indices;
    }

    private Guid? ResolveSelectedBodyId()
    {
        var selection = _store.Project.Selection;
        if (selection.Kind == CadEntityKind.Body)
        {
            return selection.EntityId;
        }

        if (selection.Kind is not CadEntityKind.Feature and not CadEntityKind.Sketch)
        {
            return null;
        }

        var body = _store.Project.Scene.Bodies.FirstOrDefault(candidate =>
            candidate.Features.Any(feature => feature.Id == selection.EntityId));

        return body?.Id;
    }

    private Guid? ResolveSelectedPlaneId()
    {
        var selection = _store.Project.Selection;
        if (selection.Kind != CadEntityKind.ReferencePlane)
        {
            return null;
        }

        return selection.EntityId;
    }

    private Guid? ResolveSelectedSketchId()
    {
        var selection = _store.Project.Selection;
        return selection.Kind == CadEntityKind.Sketch ? selection.EntityId : null;
    }

    private static IReadOnlyList<ViewportRenderSketch> BuildRenderSketches(CadProject project)
    {
        var sketches = new List<ViewportRenderSketch>();

        foreach (var body in project.Scene.Bodies)
        {
            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                var probe = BuildSketchDefinitionProbe(sketch);
                var displayConstraints = MergeDisplayConstraints(sketch.GetBasicConstraints(), sketch.Constraints);
                var constrainedIds = BuildConstrainedEntityIds(displayConstraints, sketch.Dimensions);
                var curves = BuildSketchCurves(sketch.PlaneName, sketch.Entities, constrainedIds);
                if (curves.Count == 0)
                {
                    continue;
                }

                sketches.Add(new ViewportRenderSketch
                {
                    SketchId = sketch.Id,
                    Name = sketch.Name,
                    PlaneName = sketch.PlaneName,
                    IsDraft = false,
                    IsPreview = false,
                    IsClosed = sketch.IsClosedProfile,
                    IsFullyDefined = CadProjectStore.ComputeSketchDof(probe) <= 0,
                    Curves = curves,
                    AngleDimensions = BuildAngleDimensions(sketch.PlaneName, sketch.Entities, sketch.Dimensions),
                    LinearDimensions = BuildLinearDimensions(sketch.PlaneName, sketch.Entities, sketch.Dimensions),
                    RadialDimensions = BuildRadialDimensions(sketch.PlaneName, sketch.Entities, sketch.Dimensions)
                });
            }
        }

        if (project.ActiveSketchSession is { } session)
        {
            var draftConstraints = MergeDisplayConstraints(new SketchFeature { Entities = session.DraftEntities.ToList() }.GetBasicConstraints(), session.ManualConstraints);
            var draftConstrainedIds = BuildConstrainedEntityIds(draftConstraints, session.ManualDimensions);
            var curves = BuildSketchCurves(session.PlaneName, session.DraftEntities, draftConstrainedIds);
            if (curves.Count > 0)
            {
                sketches.Add(new ViewportRenderSketch
                {
                    SketchId = session.PlaneId,
                    Name = $"Draft {session.ActiveTool}",
                    PlaneName = session.PlaneName,
                    IsDraft = true,
                    IsPreview = false,
                    IsClosed = IsClosedDraft(session.DraftEntities),
                    IsFullyDefined = CadProjectStore.ComputeSketchDof(session) <= 0,
                    Curves = curves,
                    AngleDimensions = BuildAngleDimensions(session.PlaneName, session.DraftEntities, session.ManualDimensions),
                    LinearDimensions = BuildLinearDimensions(session.PlaneName, session.DraftEntities, session.ManualDimensions),
                    RadialDimensions = BuildRadialDimensions(session.PlaneName, session.DraftEntities, session.ManualDimensions)
                });
            }

            var previewCurves = BuildSketchCurves(session.PlaneName, session.PreviewEntities, new HashSet<Guid>());
            if (previewCurves.Count > 0)
            {
                sketches.Add(new ViewportRenderSketch
                {
                    SketchId = Guid.NewGuid(),
                    Name = $"Preview {session.ActiveTool}",
                    PlaneName = session.PlaneName,
                    IsDraft = false,
                    IsPreview = true,
                    IsClosed = IsClosedDraft(session.PreviewEntities),
                    IsFullyDefined = false,
                    Curves = previewCurves
                });
            }
        }

        return sketches;
    }

    private static IReadOnlyList<ViewportRenderSketchCurve> BuildSketchCurves(
        string planeName,
        IReadOnlyList<CadSketchEntity> entities,
        IReadOnlySet<Guid> constrainedIds)
    {
        var curves = new List<ViewportRenderSketchCurve>();
        foreach (var entity in entities)
        {
            var points = entity switch
            {
                CadSketchLine line => BuildLinePoints(planeName, line),
                CadSketchRectangle rectangle => BuildRectanglePoints(planeName, rectangle),
                CadSketchCircle circle => BuildCirclePoints(planeName, circle),
                CadSketchArc arc => BuildArcPoints(planeName, arc),
                CadSketchPoint point => BuildPointPoints(planeName, point),
                CadSketchPolygon polygon => BuildPolygonPoints(planeName, polygon),
                CadSketchSlot slot => BuildSlotPoints(planeName, slot),
                CadSketchSpline spline => BuildSplinePoints(planeName, spline),
                _ => []
            };

            if (points.Length == 0)
            {
                continue;
            }

            curves.Add(new ViewportRenderSketchCurve
            {
                Points = points,
                Kind = entity is CadSketchPoint ? "point" : "polyline",
                Closed = entity is CadSketchCircle or CadSketchRectangle or CadSketchPolygon or CadSketchSlot or CadSketchPoint,
                IsConstruction = entity.IsConstruction,
                IsConstrained = constrainedIds.Contains(entity.Id),
                EntityId = entity.Id,
                EntityType = entity.EntityType
            });
        }

        return curves;
    }

    private static CadSketchSession BuildSketchDefinitionProbe(SketchFeature sketch)
    {
        return new CadSketchSession
        {
            PlaneId = sketch.Id,
            PlaneName = sketch.PlaneName,
            DraftEntities = sketch.Entities.ToList(),
            ManualConstraints = sketch.Constraints.ToList(),
            ManualDimensions = sketch.Dimensions.ToList()
        };
    }

    private static IReadOnlySet<Guid> BuildConstrainedEntityIds(
        IReadOnlyList<CadSketchConstraint> constraints,
        IReadOnlyList<CadSketchDimension> dimensions)
    {
        var ids = new HashSet<Guid>();
        foreach (var constraint in constraints)
        {
            foreach (var id in constraint.EntityIds)
            {
                ids.Add(id);
            }
        }

        foreach (var dimension in dimensions)
        {
            if (dimension.IsDriven)
            {
                continue;
            }

            foreach (var id in dimension.EntityIds)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static IReadOnlyList<CadSketchConstraint> MergeDisplayConstraints(
        IReadOnlyList<CadSketchConstraint> inferred,
        IReadOnlyList<CadSketchConstraint> manual)
    {
        var merged = new List<CadSketchConstraint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var constraint in inferred.Concat(manual))
        {
            var key = ConstraintDisplayKey(constraint);
            if (seen.Add(key))
            {
                merged.Add(constraint);
            }
        }

        return merged;
    }

    private static string ConstraintDisplayKey(CadSketchConstraint constraint)
    {
        var ids = constraint.EntityIds
            .OrderBy(id => id)
            .Select(id => id.ToString("N"));
        return $"{constraint.Kind}:{string.Join(",", ids)}";
    }

    private static bool IsClosedDraft(IReadOnlyList<CadSketchEntity> entities)
    {
        if (entities.Count == 0)
        {
            return false;
        }

        if (entities.Count == 1)
        {
            return entities[0] is CadSketchRectangle or CadSketchCircle or CadSketchPolygon or CadSketchSlot;
        }

        if (entities.Any(entity => entity is not CadSketchLine))
        {
            return false;
        }

        var lines = entities.Cast<CadSketchLine>().ToList();
        if (lines.Count < 3)
        {
            return false;
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var current = lines[index];
            var next = lines[(index + 1) % lines.Count];
            if (!PointsEqual(current.EndX, current.EndY, next.StartX, next.StartY))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PointsEqual(double ax, double ay, double bx, double by)
    {
        const double tolerance = 0.0001d;
        return Math.Abs(ax - bx) <= tolerance && Math.Abs(ay - by) <= tolerance;
    }

    private static double[] BuildLinePoints(string planeName, CadSketchLine line)
    {
        return
        [
            .. MapPlanePoint(planeName, line.StartX, line.StartY),
            .. MapPlanePoint(planeName, line.EndX, line.EndY)
        ];
    }

    private static double[] BuildRectanglePoints(string planeName, CadSketchRectangle rectangle)
    {
        var x0 = rectangle.X;
        var y0 = rectangle.Y;
        var x1 = rectangle.X + rectangle.Width;
        var y1 = rectangle.Y + rectangle.Height;
        return
        [
            .. MapPlanePoint(planeName, x0, y0),
            .. MapPlanePoint(planeName, x1, y0),
            .. MapPlanePoint(planeName, x1, y1),
            .. MapPlanePoint(planeName, x0, y1),
            .. MapPlanePoint(planeName, x0, y0)
        ];
    }

    private static double[] BuildCirclePoints(string planeName, CadSketchCircle circle)
    {
        const int segments = 48;
        var points = new double[(segments + 1) * 3];
        for (var index = 0; index <= segments; index++)
        {
            var angle = (Math.PI * 2d * index) / segments;
            var x = circle.CenterX + Math.Cos(angle) * circle.Radius;
            var y = circle.CenterY + Math.Sin(angle) * circle.Radius;
            var world = MapPlanePoint(planeName, x, y);
            var target = index * 3;
            points[target] = world[0];
            points[target + 1] = world[1];
            points[target + 2] = world[2];
        }

        return points;
    }

    private static double[] BuildArcPoints(string planeName, CadSketchArc arc)
    {
        const int segments = 40;
        var sweep = ComputeArcSweep(arc.StartAngleDegrees, arc.EndAngleDegrees, arc.CounterClockwise);
        var points = new double[(segments + 1) * 3];
        for (var index = 0; index <= segments; index++)
        {
            var t = index / (double)segments;
            var angle = DegreesToRadians(arc.StartAngleDegrees + (sweep * t));
            var x = arc.CenterX + Math.Cos(angle) * arc.Radius;
            var y = arc.CenterY + Math.Sin(angle) * arc.Radius;
            var world = MapPlanePoint(planeName, x, y);
            var target = index * 3;
            points[target] = world[0];
            points[target + 1] = world[1];
            points[target + 2] = world[2];
        }

        return points;
    }

    private static double[] BuildPointPoints(string planeName, CadSketchPoint point)
    {
        return MapPlanePoint(planeName, point.X, point.Y);
    }

    private static double[] BuildPolygonPoints(string planeName, CadSketchPolygon polygon)
    {
        var sides = Math.Max(3, polygon.Sides);
        var points = new double[(sides + 1) * 3];
        for (var index = 0; index <= sides; index++)
        {
            var angle = (-Math.PI / 2d) + (Math.PI * 2d * index) / sides;
            var x = polygon.CenterX + Math.Cos(angle) * polygon.Radius;
            var y = polygon.CenterY + Math.Sin(angle) * polygon.Radius;
            var world = MapPlanePoint(planeName, x, y);
            var target = index * 3;
            points[target] = world[0];
            points[target + 1] = world[1];
            points[target + 2] = world[2];
        }

        return points;
    }

    private static double[] BuildSlotPoints(string planeName, CadSketchSlot slot)
    {
        const int capSegments = 24;
        var dx = slot.Center2X - slot.Center1X;
        var dy = slot.Center2Y - slot.Center1Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var axisAngle = len < 1e-9 ? 0d : Math.Atan2(dy, dx);

        var ptList = new List<double[]>();

        for (var i = 0; i <= capSegments; i++)
        {
            var t = (double)i / capSegments;
            var angle = axisAngle + Math.PI / 2d + Math.PI * t;
            ptList.Add(MapPlanePoint(planeName,
                slot.Center1X + Math.Cos(angle) * slot.Radius,
                slot.Center1Y + Math.Sin(angle) * slot.Radius));
        }

        for (var i = 0; i <= capSegments; i++)
        {
            var t = (double)i / capSegments;
            var angle = axisAngle + 3d * Math.PI / 2d + Math.PI * t;
            ptList.Add(MapPlanePoint(planeName,
                slot.Center2X + Math.Cos(angle) * slot.Radius,
                slot.Center2Y + Math.Sin(angle) * slot.Radius));
        }

        ptList.Add(ptList[0]);

        var result = new double[ptList.Count * 3];
        for (var i = 0; i < ptList.Count; i++)
        {
            result[i * 3] = ptList[i][0];
            result[i * 3 + 1] = ptList[i][1];
            result[i * 3 + 2] = ptList[i][2];
        }

        return result;
    }

    private static double[] BuildSplinePoints(string planeName, CadSketchSpline spline)
    {
        var coords = spline.ControlPointsXY;
        var count = coords.Count / 2;
        if (count < 2)
        {
            return [];
        }

        var points = new double[count * 3];
        for (var index = 0; index < count; index++)
        {
            var world = MapPlanePoint(planeName, coords[index * 2], coords[index * 2 + 1]);
            points[index * 3] = world[0];
            points[index * 3 + 1] = world[1];
            points[index * 3 + 2] = world[2];
        }

        return points;
    }

    private static double[] MapPlanePoint(string planeName, double u, double v)
    {
        return planeName.Trim().ToLowerInvariant() switch
        {
            "front" => [0d, u, v],
            "right" => [u, 0d, v],
            _ => [u, v, 0d]
        };
    }

    private static IReadOnlyList<ViewportRenderAngleDimension> BuildAngleDimensions(
        string planeName,
        IReadOnlyList<CadSketchEntity> entities,
        IReadOnlyList<CadSketchDimension> dimensions)
    {
        var result = new List<ViewportRenderAngleDimension>();

        foreach (var dim in dimensions)
        {
            if (dim.Kind != CadSketchDimensionKind.Angle || dim.EntityIds.Count < 2)
            {
                continue;
            }

            var lineA = entities.OfType<CadSketchLine>().FirstOrDefault(l => l.Id == dim.EntityIds[0]);
            var lineB = entities.OfType<CadSketchLine>().FirstOrDefault(l => l.Id == dim.EntityIds[1]);
            if (lineA is null || lineB is null)
            {
                continue;
            }

            if (!TryLineIntersection(lineA, lineB, out var ix, out var iy))
            {
                continue;
            }

            var dax = lineA.EndX - lineA.StartX;
            var day = lineA.EndY - lineA.StartY;
            var dbx = lineB.EndX - lineB.StartX;
            var dby = lineB.EndY - lineB.StartY;

            var lenA = Math.Sqrt(dax * dax + day * day);
            var lenB = Math.Sqrt(dbx * dbx + dby * dby);
            var arcRadius = Math.Clamp(Math.Min(lenA, lenB) * 0.18, 0.4, 2.5);

            // Direction from intersection toward the body of each line
            var midA = new double[] { (lineA.StartX + lineA.EndX) / 2, (lineA.StartY + lineA.EndY) / 2 };
            var midB = new double[] { (lineB.StartX + lineB.EndX) / 2, (lineB.StartY + lineB.EndY) / 2 };
            var dirAx = midA[0] - ix;
            var dirAy = midA[1] - iy;
            var dirBx = midB[0] - ix;
            var dirBy = midB[1] - iy;

            var startDeg = Math.Atan2(dirAy, dirAx) * 180.0 / Math.PI;
            var endDeg = Math.Atan2(dirBy, dirBx) * 180.0 / Math.PI;

            // Normalize sweep to the acute/obtuse angle based on dim.Value
            var sweep = endDeg - startDeg;
            while (sweep > 180.0) sweep -= 360.0;
            while (sweep < -180.0) sweep += 360.0;

            var world = MapPlanePoint(planeName, ix, iy);
            result.Add(new ViewportRenderAngleDimension
            {
                Cx = world[0],
                Cy = world[1],
                Cz = world[2],
                StartDeg = startDeg,
                SweepDeg = sweep,
                Radius = arcRadius,
                Label = dim.Label
            });
        }

        return result;
    }

    private static IReadOnlyList<ViewportRenderLinearDimension> BuildLinearDimensions(
        string planeName,
        IReadOnlyList<CadSketchEntity> entities,
        IReadOnlyList<CadSketchDimension> dimensions)
    {
        var result = new List<ViewportRenderLinearDimension>();

        foreach (var dim in dimensions)
        {
            if (dim.Kind != CadSketchDimensionKind.Length || dim.EntityIds.Count == 0)
                continue;

            var line = entities.OfType<CadSketchLine>().FirstOrDefault(l => l.Id == dim.EntityIds[0]);
            if (line is null)
                continue;

            var p1 = MapPlanePoint(planeName, line.StartX, line.StartY);
            var p2 = MapPlanePoint(planeName, line.EndX, line.EndY);

            // Perpendicular offset vector (in sketch plane, then mapped)
            var dx = line.EndX - line.StartX;
            var dy = line.EndY - line.StartY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) continue;

            // Perpendicular to line direction in 2D sketch coords, then map
            const double OffsetScale = 1.2;
            var perpU = -dy / len * OffsetScale;
            var perpV = dx / len * OffsetScale;
            var off = MapPlanePoint(planeName, perpU, perpV);

            result.Add(new ViewportRenderLinearDimension
            {
                P1X = p1[0], P1Y = p1[1], P1Z = p1[2],
                P2X = p2[0], P2Y = p2[1], P2Z = p2[2],
                Ox = off[0], Oy = off[1], Oz = off[2],
                Label = $"{dim.Value:0.###}",
                IsDriven = dim.IsDriven
            });
        }

        return result;
    }

    private static IReadOnlyList<ViewportRenderRadialDimension> BuildRadialDimensions(
        string planeName,
        IReadOnlyList<CadSketchEntity> entities,
        IReadOnlyList<CadSketchDimension> dimensions)
    {
        var result = new List<ViewportRenderRadialDimension>();

        foreach (var dim in dimensions)
        {
            if (dim.Kind is not (CadSketchDimensionKind.Radius or CadSketchDimensionKind.Diameter)
                || dim.EntityIds.Count == 0)
                continue;

            double cx, cy, radius;
            bool isDiameter;

            var entity = entities.FirstOrDefault(e => e.Id == dim.EntityIds[0]);
            switch (entity)
            {
                case CadSketchCircle circle:
                    cx = circle.CenterX; cy = circle.CenterY; radius = circle.Radius;
                    isDiameter = dim.Kind == CadSketchDimensionKind.Diameter;
                    break;
                case CadSketchArc arc:
                    cx = arc.CenterX; cy = arc.CenterY; radius = arc.Radius;
                    isDiameter = false;
                    break;
                default:
                    continue;
            }

            // Leader points toward 45° on the sketch plane
            const double LeaderAngle = Math.PI / 4;
            var eu = cx + Math.Cos(LeaderAngle) * radius;
            var ev = cy + Math.Sin(LeaderAngle) * radius;

            var cWorld = MapPlanePoint(planeName, cx, cy);
            var eWorld = MapPlanePoint(planeName, eu, ev);

            result.Add(new ViewportRenderRadialDimension
            {
                Cx = cWorld[0], Cy = cWorld[1], Cz = cWorld[2],
                Ex = eWorld[0], Ey = eWorld[1], Ez = eWorld[2],
                Label = isDiameter ? $"⌀{dim.Value * 2:0.###}" : $"R{dim.Value:0.###}",
                IsDriven = dim.IsDriven,
                IsDiameter = isDiameter
            });
        }

        return result;
    }

    private static bool TryLineIntersection(CadSketchLine a, CadSketchLine b, out double ix, out double iy)
    {
        var dax = a.EndX - a.StartX;
        var day = a.EndY - a.StartY;
        var dbx = b.EndX - b.StartX;
        var dby = b.EndY - b.StartY;
        var cross = dax * dby - day * dbx;
        if (Math.Abs(cross) < 1e-10)
        {
            ix = iy = 0;
            return false;
        }

        var dx = b.StartX - a.StartX;
        var dy = b.StartY - a.StartY;
        var t = (dx * dby - dy * dbx) / cross;
        ix = a.StartX + t * dax;
        iy = a.StartY + t * day;
        return true;
    }

    private CadCommandAction? TranslateCommand(CadViewportCommand command)
    {
        return command.Kind switch
        {
            CadViewportCommandKind.AddPrimitive when TryMapPrimitive(command.PrimitiveKind, out var primitiveKind) =>
                new CadCommandAction(CadCommandActionKind.CreatePrimitive, PrimitiveKind: primitiveKind),

            CadViewportCommandKind.MoveSelected when TryMapAxis(command.Axis, out var axis) =>
                new CadCommandAction(CadCommandActionKind.MoveSelected, Axis: axis, Amount: command.Distance),

            CadViewportCommandKind.DeleteSelected =>
                new CadCommandAction(CadCommandActionKind.DeleteSelection),

            CadViewportCommandKind.SelectPlane when TryMapPlane(command.Plane, out var planeKind) =>
                new CadCommandAction(CadCommandActionKind.SelectPlane, PlaneKind: planeKind, EntityName: command.Plane),

            CadViewportCommandKind.StartSketch when TryMapPlane(command.Plane, out var sketchPlaneKind) =>
                new CadCommandAction(CadCommandActionKind.StartSketch, PlaneKind: sketchPlaneKind, EntityName: command.Plane),

            CadViewportCommandKind.StartSketch =>
                new CadCommandAction(CadCommandActionKind.StartSketch),

            CadViewportCommandKind.CancelSketch =>
                new CadCommandAction(CadCommandActionKind.CancelSketch),

            CadViewportCommandKind.SetSketchTool when TryMapSketchTool(command.SketchTool, out var sketchTool) =>
                new CadCommandAction(CadCommandActionKind.SetSketchTool, SketchTool: sketchTool, Amount: command.Distance),

            CadViewportCommandKind.UpdateSketchPreview =>
                new CadCommandAction(CadCommandActionKind.UpdateSketchPreview, U: command.U, V: command.V),

            CadViewportCommandKind.ClearSketchPreview =>
                new CadCommandAction(CadCommandActionKind.ClearSketchPreview),

            CadViewportCommandKind.CancelSketchStep =>
                new CadCommandAction(CadCommandActionKind.CancelSketchStep),

            CadViewportCommandKind.PlaceSketchAt =>
                new CadCommandAction(CadCommandActionKind.PlaceSketchEntity, U: command.U, V: command.V),

            CadViewportCommandKind.FinishSketch =>
                new CadCommandAction(CadCommandActionKind.FinishSketch),

            CadViewportCommandKind.ApplySketchConstraint when !string.IsNullOrWhiteSpace(command.ConstraintName) =>
                new CadCommandAction(CadCommandActionKind.ApplySketchConstraint, EntityName: command.ConstraintName),

            CadViewportCommandKind.ExtrudeSketch =>
                new CadCommandAction(
                    CadCommandActionKind.ExtrudeSelectedSketch,
                    Amount: command.Distance,
                    ExtrudeOp: command.ExtrudeOp is not null && Enum.TryParse<CadExtrudeOperation>(command.ExtrudeOp, out var extrudeOp)
                        ? extrudeOp
                        : CadExtrudeOperation.NewBody,
                    TargetBodyId: command.TargetBodyId),

            CadViewportCommandKind.RevolveSketch when TryMapAxis(string.IsNullOrWhiteSpace(command.Axis) ? "y" : command.Axis, out var revolveAxis) =>
                new CadCommandAction(CadCommandActionKind.RevolveSelectedSketch, Amount: command.Distance, Axis: revolveAxis),

            CadViewportCommandKind.SweepSketch =>
                new CadCommandAction(CadCommandActionKind.SweepSelectedSketch, Amount: command.Distance, U: command.U),

            CadViewportCommandKind.LoftProfiles =>
                new CadCommandAction(CadCommandActionKind.LoftFromProfiles, Amount: command.Distance, EntityId: command.EntityIdA, EntityIdB: command.EntityIdB),

            CadViewportCommandKind.FilletBody =>
                new CadCommandAction(CadCommandActionKind.FilletSelectedBody, Amount: command.Distance),

            CadViewportCommandKind.ChamferBody =>
                new CadCommandAction(CadCommandActionKind.ChamferSelectedBody, Amount: command.Distance),

            CadViewportCommandKind.ShellBody =>
                new CadCommandAction(CadCommandActionKind.ShellSelectedBody, Amount: command.Distance),

            CadViewportCommandKind.MirrorBody when TryMapAxis(command.Axis, out var mirrorAxis) =>
                new CadCommandAction(CadCommandActionKind.MirrorSelectedBody, Axis: mirrorAxis),

            CadViewportCommandKind.LinearPatternBody when TryMapAxis(command.Axis, out var patternAxis) =>
                new CadCommandAction(CadCommandActionKind.LinearPatternSelectedBody, Amount: command.Distance, U: command.U, Axis: patternAxis),

            CadViewportCommandKind.CircularPatternBody when TryMapAxis(command.Axis, out var circularAxis) =>
                new CadCommandAction(CadCommandActionKind.CircularPatternSelectedBody, Amount: command.Distance, U: command.U, Axis: circularAxis),

            CadViewportCommandKind.HoleBody =>
                new CadCommandAction(CadCommandActionKind.HoleSelectedBody, Amount: command.Distance, U: command.U, V: command.V, EntityName: command.Axis),

            CadViewportCommandKind.BooleanUnion =>
                new CadCommandAction(CadCommandActionKind.BooleanUnion, EntityId: command.EntityIdA, EntityIdB: command.EntityIdB),

            CadViewportCommandKind.BooleanSubtract =>
                new CadCommandAction(CadCommandActionKind.BooleanSubtract, EntityId: command.EntityIdA, EntityIdB: command.EntityIdB),

            CadViewportCommandKind.BooleanIntersect =>
                new CadCommandAction(CadCommandActionKind.BooleanIntersect, EntityId: command.EntityIdA, EntityIdB: command.EntityIdB),

            _ => null
        };
    }

    private bool TryApplyBodyParameter(CadBody body, string key, double value)
    {
        if (string.Equals(key, "X", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Y", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Z", StringComparison.OrdinalIgnoreCase))
        {
            var translation = GetBodyTranslation(body);
            var x = string.Equals(key, "X", StringComparison.OrdinalIgnoreCase) ? value : translation.X;
            var y = string.Equals(key, "Y", StringComparison.OrdinalIgnoreCase) ? value : translation.Y;
            var z = string.Equals(key, "Z", StringComparison.OrdinalIgnoreCase) ? value : translation.Z;
            SetBodyTranslation(body, x, y, z);
            return true;
        }

        var createFeature = body.BaseFeature;
        return createFeature is not null && createFeature.TrySetParameter(key, value);
    }

    public (int Count, double TotalAngle, string Axis)? FindCircularPatternParams(Guid featureId)
    {
        var feature = FindSelectedFeature(featureId) as CircularPatternFeature;
        if (feature is null)
        {
            return null;
        }

        return (feature.Count, feature.TotalAngle, feature.Axis.ToString().ToLowerInvariant());
    }

    public StudioWorkspaceActionResult EditCircularPatternFeature(Guid featureId, int count, double totalAngle, string axis)
    {
        PushUndoSnapshot();
        if (!TryMapAxis(axis, out var cadAxis))
        {
            cadAxis = CadAxis.Y;
        }

        (CircularPatternFeature Feature, CadBody Body)? found = null;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as CircularPatternFeature;
            if (f is not null)
            {
                found = (f, body);
                break;
            }
        }

        if (found is null)
        {
            return PublishFailure("Circular pattern feature not found.");
        }

        var cp = found.Value.Feature;
        cp.Count = Math.Max(2, count);
        cp.TotalAngle = Math.Max(1d, totalAngle);
        cp.Axis = cadAxis;
        cp.Name = $"Circular Pattern ({cp.Count} × {found.Value.Body.Name})";

        return PublishSuccess($"Updated circular pattern: {cp.Count} copies, {cp.TotalAngle:0.###}° around {cadAxis}.", true);
    }

    public (int Count, double Spacing, string Axis)? FindLinearPatternParams(Guid featureId)
    {
        var feature = FindSelectedFeature(featureId) as LinearPatternFeature;
        if (feature is null)
        {
            return null;
        }

        return (feature.Count, feature.Spacing, feature.Axis.ToString().ToLowerInvariant());
    }

    public StudioWorkspaceActionResult EditLinearPatternFeature(Guid featureId, int count, double spacing, string axis)
    {
        PushUndoSnapshot();
        if (!TryMapAxis(axis, out var cadAxis))
        {
            cadAxis = CadAxis.X;
        }

        (LinearPatternFeature Feature, CadBody Body)? found = null;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as LinearPatternFeature;
            if (f is not null)
            {
                found = (f, body);
                break;
            }
        }

        if (found is null)
        {
            return PublishFailure("Linear pattern feature not found.");
        }

        var lp = found.Value.Feature;
        lp.Count = Math.Max(2, count);
        lp.Spacing = Math.Max(0.1d, spacing);
        lp.Axis = cadAxis;
        lp.Name = $"Linear Pattern ({lp.Count} × {found.Value.Body.Name})";

        return PublishSuccess($"Updated linear pattern: {lp.Count} copies, {lp.Spacing:0.###}mm spacing along {cadAxis}.", true);
    }

    public double? FindFilletRadius(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as FilletFeature;
        return f?.Radius;
    }

    public StudioWorkspaceActionResult EditFilletFeature(Guid featureId, double radius)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as FilletFeature;
            if (f is not null)
            {
                f.Radius = Math.Max(0.01d, radius);
                f.Name = $"Fillet (r={f.Radius:0.###})";
                return PublishSuccess($"Updated fillet radius to {f.Radius:0.###} mm.", true);
            }
        }

        return PublishFailure("Fillet feature not found.");
    }

    public double? FindChamferDistance(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as ChamferFeature;
        return f?.Distance;
    }

    public StudioWorkspaceActionResult EditChamferFeature(Guid featureId, double distance)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as ChamferFeature;
            if (f is not null)
            {
                f.Distance = Math.Max(0.01d, distance);
                f.Name = $"Chamfer (d={f.Distance:0.###})";
                return PublishSuccess($"Updated chamfer distance to {f.Distance:0.###} mm.", true);
            }
        }

        return PublishFailure("Chamfer feature not found.");
    }

    public double? FindShellThickness(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as ShellFeature;
        return f?.Thickness;
    }

    public StudioWorkspaceActionResult EditShellFeature(Guid featureId, double thickness)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as ShellFeature;
            if (f is not null)
            {
                f.Thickness = Math.Max(0.1d, thickness);
                f.Name = $"Shell (wall={f.Thickness:0.###})";
                return PublishSuccess($"Updated shell thickness to {f.Thickness:0.###} mm.", true);
            }
        }

        return PublishFailure("Shell feature not found.");
    }

    public StudioWorkspaceActionResult RenameBody(Guid bodyId, string newName)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.RenameBody,
            EntityId: bodyId,
            EntityName: newName));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult DuplicateSelectedBody()
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(CadCommandActionKind.DuplicateSelectedBody));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult SetBodyColor(Guid bodyId, string? color)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.SetBodyColor,
            EntityId: bodyId,
            EntityName: color ?? string.Empty));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult ToggleBodyVisibility(Guid bodyId)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.ToggleBodyVisibility,
            EntityId: bodyId));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult CreateDatumPlane(CadReferencePlaneKind sourceKind, double offset, string name)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.CreateDatumPlane,
            PlaneKind: sourceKind,
            Amount: offset,
            EntityName: name));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult ToggleDatumPlaneVisibility(Guid planeId)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.ToggleDatumPlaneVisibility,
            EntityId: planeId));
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult DeleteDatumPlane(Guid planeId)
    {
        PushUndoSnapshot();
        var result = _store.Apply(new CadCommandAction(
            CadCommandActionKind.DeleteDatumPlane,
            EntityId: planeId));
        return PublishCadResult(result);
    }

    public IReadOnlyList<CadReferencePlane> GetDatumPlanes() =>
        _store.Project.Scene.ReferencePlanes
            .Where(p => p.Kind == CadReferencePlaneKind.Datum)
            .ToList();

    public (double Depth, bool ReverseDirection, CadExtrudeOperation Operation, Guid TargetBodyId)? FindExtrudeParams(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as ExtrudeFeature;
        if (f is null) return null;
        return (f.Depth, f.ReverseDirection, f.Operation, f.TargetBodyId);
    }

    public StudioWorkspaceActionResult EditExtrudeFeature(Guid featureId, double depth, bool reverseDirection, CadExtrudeOperation operation, Guid targetBodyId)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as ExtrudeFeature;
            if (f is not null)
            {
                f.Depth = Math.Max(0.01d, depth);
                f.ReverseDirection = reverseDirection;
                f.Operation = operation;
                f.TargetBodyId = targetBodyId;
                f.Name = $"Extrude ({f.Depth:0.###} mm)";
                return PublishSuccess($"Updated extrude: {f.Depth:0.###} mm.", true);
            }
        }

        return PublishFailure("Extrude feature not found.");
    }

    public (double AngleDegrees, string Axis)? FindRevolveParams(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as RevolveFeature;
        if (f is null) return null;
        return (f.AngleDegrees, f.Axis.ToString().ToLowerInvariant());
    }

    public StudioWorkspaceActionResult EditRevolveFeature(Guid featureId, double angleDegrees, string axis)
    {
        PushUndoSnapshot();
        if (!TryMapAxis(axis, out var cadAxis)) cadAxis = CadAxis.Y;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as RevolveFeature;
            if (f is not null)
            {
                f.AngleDegrees = Math.Clamp(angleDegrees, 1d, 360d);
                f.Axis = cadAxis;
                f.Name = $"Revolve ({f.AngleDegrees:0.###}° around {cadAxis})";
                return PublishSuccess($"Updated revolve: {f.AngleDegrees:0.###}° around {cadAxis}.", true);
            }
        }

        return PublishFailure("Revolve feature not found.");
    }

    public string? FindMirrorParams(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as MirrorFeature;
        return f?.Axis.ToString().ToLowerInvariant();
    }

    public StudioWorkspaceActionResult EditMirrorFeature(Guid featureId, string axis)
    {
        PushUndoSnapshot();
        if (!TryMapAxis(axis, out var cadAxis)) cadAxis = CadAxis.X;
        var mirrorAxis = cadAxis switch { CadAxis.Y => MirrorAxis.Y, CadAxis.Z => MirrorAxis.Z, _ => MirrorAxis.X };
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as MirrorFeature;
            if (f is not null)
            {
                f.Axis = mirrorAxis;
                f.Name = $"Mirror ({mirrorAxis})";
                return PublishSuccess($"Updated mirror: {mirrorAxis} plane.", true);
            }
        }

        return PublishFailure("Mirror feature not found.");
    }

    public IReadOnlyList<(Guid Id, string Name)> GetAvailableClosedSketches()
        => _store.Project.Scene.Bodies
            .SelectMany(b => b.Features.OfType<SketchFeature>())
            .Where(s => s.IsClosedProfile)
            .Select(s => (s.Id, s.Name))
            .ToList();

    public (Guid ProfileAId, string ProfileAName, Guid ProfileBId, string ProfileBName, double Distance)? FindLoftParams(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as LoftFeature;
        if (f is null) return null;
        return (f.ProfileASketchId, f.ProfileASketchName, f.ProfileBSketchId, f.ProfileBSketchName, f.Distance);
    }

    public StudioWorkspaceActionResult EditLoftFeature(Guid featureId, Guid profileAId, Guid profileBId, double distance)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as LoftFeature;
            if (f is not null)
            {
                f.ProfileASketchId = profileAId;
                f.ProfileBSketchId = profileBId;
                f.Distance = Math.Clamp(distance, 0.1d, 10000d);
                return PublishSuccess($"Updated loft: distance={f.Distance:0.###}.", true);
            }
        }

        return PublishFailure("Loft feature not found.");
    }

    public (double Distance, double TwistDegrees)? FindSweepParams(Guid featureId)
    {
        var f = FindSelectedFeature(featureId) as SweepFeature;
        if (f is null) return null;
        return (f.Distance, f.TwistDegrees);
    }

    public StudioWorkspaceActionResult EditSweepFeature(Guid featureId, double distance, double twistDegrees)
    {
        PushUndoSnapshot();
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as SweepFeature;
            if (f is not null)
            {
                f.Distance = Math.Clamp(distance, 0.1d, 10000d);
                f.TwistDegrees = Math.Clamp(twistDegrees, -360d, 360d);
                f.Name = $"Sweep ({f.Distance:0.###})";
                return PublishSuccess($"Updated sweep: distance={f.Distance:0.###}, twist={f.TwistDegrees:0.###}°.", true);
            }
        }

        return PublishFailure("Sweep feature not found.");
    }

    public (double Diameter, string DepthKind, double DepthValue, double CenterOffsetX, double CenterOffsetY)? FindHoleParams(Guid featureId)
    {
        var feature = FindSelectedFeature(featureId) as HoleFeature;
        if (feature is null)
        {
            return null;
        }

        return (feature.Diameter, feature.DepthKind.ToString(), feature.DepthValue, feature.CenterOffsetX, feature.CenterOffsetY);
    }

    public StudioWorkspaceActionResult EditHoleFeature(Guid featureId, double diameter, string depthKind, double depthValue, double centerOffsetX, double centerOffsetY)
    {
        PushUndoSnapshot();
        HoleFeature? found = null;
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var f = body.Features.FirstOrDefault(item => item.Id == featureId) as HoleFeature;
            if (f is not null)
            {
                found = f;
                break;
            }
        }

        if (found is null)
        {
            return PublishFailure("Hole feature not found.");
        }

        found.Diameter = Math.Max(0.01d, diameter);
        found.DepthKind = string.Equals(depthKind, "Blind", StringComparison.OrdinalIgnoreCase)
            ? HoleDepthKind.Blind : HoleDepthKind.ThroughAll;
        found.DepthValue = Math.Max(0.01d, depthValue);
        found.CenterOffsetX = centerOffsetX;
        found.CenterOffsetY = centerOffsetY;
        var depthLabel = found.DepthKind == HoleDepthKind.ThroughAll ? "Through" : $"{found.DepthValue:0.###}mm";
        found.Name = $"Hole (⌀{found.Diameter:0.###} × {depthLabel})";

        return PublishSuccess($"Updated {found.Name}.", true);
    }

    private CadFeature? FindSelectedFeature(Guid featureId)
    {
        foreach (var body in _store.Project.Scene.Bodies)
        {
            var feature = body.Features.FirstOrDefault(item => item.Id == featureId);
            if (feature is not null)
            {
                return feature;
            }
        }

        return null;
    }

    private CadSketchEntity? FindSelectedSketchEntity(Guid entityId)
    {
        foreach (var body in _store.Project.Scene.Bodies)
        {
            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                var entity = sketch.Entities.FirstOrDefault(item => item.Id == entityId);
                if (entity is not null)
                {
                    return entity;
                }
            }
        }

        return null;
    }

    private static Vector3D GetBodyTranslation(CadBody body)
    {
        var x = 0d;
        var y = 0d;
        var z = 0d;

        foreach (var move in body.Features.OfType<MoveFeature>())
        {
            x += move.X;
            y += move.Y;
            z += move.Z;
        }

        return new Vector3D(x, y, z);
    }

    private static void SetBodyTranslation(CadBody body, double targetX, double targetY, double targetZ)
    {
        var moveFeatures = body.Features.OfType<MoveFeature>().ToList();
        if (moveFeatures.Count == 0)
        {
            body.Features.Add(new MoveFeature
            {
                Name = $"Move{body.Features.Count(feature => feature is MoveFeature) + 1}",
                X = targetX,
                Y = targetY,
                Z = targetZ
            });
            return;
        }

        var previousX = 0d;
        var previousY = 0d;
        var previousZ = 0d;
        for (var index = 0; index < moveFeatures.Count - 1; index++)
        {
            previousX += moveFeatures[index].X;
            previousY += moveFeatures[index].Y;
            previousZ += moveFeatures[index].Z;
        }

        var lastMove = moveFeatures[^1];
        lastMove.X = targetX - previousX;
        lastMove.Y = targetY - previousY;
        lastMove.Z = targetZ - previousZ;
    }

    private static bool TryParseNumber(string rawValue, out double value)
    {
        var normalized = (rawValue ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryMapPrimitive(string? raw, out CadPrimitiveKind primitiveKind)
    {
        primitiveKind = CadPrimitiveKind.Box;
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "box" => Set(out primitiveKind, CadPrimitiveKind.Box),
            "sphere" => Set(out primitiveKind, CadPrimitiveKind.Sphere),
            "cylinder" => Set(out primitiveKind, CadPrimitiveKind.Cylinder),
            "cone" => Set(out primitiveKind, CadPrimitiveKind.Cone),
            "torus" => Set(out primitiveKind, CadPrimitiveKind.Torus),
            "pyramid" => Set(out primitiveKind, CadPrimitiveKind.Pyramid),
            "wedge" => Set(out primitiveKind, CadPrimitiveKind.Wedge),
            "ellipsoid" => Set(out primitiveKind, CadPrimitiveKind.Ellipsoid),
            "capsule" => Set(out primitiveKind, CadPrimitiveKind.Capsule),
            "hemisphere" => Set(out primitiveKind, CadPrimitiveKind.Hemisphere),
            "prism" => Set(out primitiveKind, CadPrimitiveKind.Prism),
            "arrow" => Set(out primitiveKind, CadPrimitiveKind.Arrow),
            "icosphere" => Set(out primitiveKind, CadPrimitiveKind.Icosphere),
            "tetrahedron" => Set(out primitiveKind, CadPrimitiveKind.Tetrahedron),
            "octahedron" => Set(out primitiveKind, CadPrimitiveKind.Octahedron),
            "icosahedron" => Set(out primitiveKind, CadPrimitiveKind.Icosahedron),
            _ => false
        };
    }

    private static bool TryMapPlane(string? raw, out CadReferencePlaneKind planeKind)
    {
        planeKind = CadReferencePlaneKind.Top;
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "top" => Set(out planeKind, CadReferencePlaneKind.Top),
            "front" => Set(out planeKind, CadReferencePlaneKind.Front),
            "right" => Set(out planeKind, CadReferencePlaneKind.Right),
            _ => false
        };
    }

    private static bool TryMapAxis(string? raw, out CadAxis axis)
    {
        axis = CadAxis.X;
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "x" => Set(out axis, CadAxis.X),
            "y" => Set(out axis, CadAxis.Y),
            "z" => Set(out axis, CadAxis.Z),
            _ => false
        };
    }

    private static bool TryMapSketchTool(string? raw, out CadSketchToolKind tool)
    {
        tool = CadSketchToolKind.Rectangle;
        return (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "line" => Set(out tool, CadSketchToolKind.Line),
            "rectangle" => Set(out tool, CadSketchToolKind.Rectangle),
            "circle" => Set(out tool, CadSketchToolKind.Circle),
            "arc" => Set(out tool, CadSketchToolKind.Arc),
            "point" => Set(out tool, CadSketchToolKind.Point),
            "polygon" => Set(out tool, CadSketchToolKind.Polygon),
            "slot" => Set(out tool, CadSketchToolKind.Slot),
            "spline" => Set(out tool, CadSketchToolKind.Spline),
            "mirror" => Set(out tool, CadSketchToolKind.Mirror),
            "trim" => Set(out tool, CadSketchToolKind.Trim),
            "offset" => Set(out tool, CadSketchToolKind.Offset),
            "fillet2d" => Set(out tool, CadSketchToolKind.Fillet2d),
            "transform" => Set(out tool, CadSketchToolKind.Transform),
            "rotate" => Set(out tool, CadSketchToolKind.Rotate),
            "angledimension" => Set(out tool, CadSketchToolKind.AngleDimension),
            "lineardimension" => Set(out tool, CadSketchToolKind.LinearDimension),
            "radiusdimension" => Set(out tool, CadSketchToolKind.RadiusDimension),
            _ => false
        };
    }

    private static void TranslateSketchEntities(IReadOnlyList<CadSketchEntity> entities, double du, double dv)
    {
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CadSketchLine line:
                    line.StartX += du;
                    line.StartY += dv;
                    line.EndX += du;
                    line.EndY += dv;
                    break;
                case CadSketchRectangle rect:
                    rect.X += du;
                    rect.Y += dv;
                    break;
                case CadSketchCircle circle:
                    circle.CenterX += du;
                    circle.CenterY += dv;
                    break;
                case CadSketchArc arc:
                    arc.CenterX += du;
                    arc.CenterY += dv;
                    break;
                case CadSketchPoint point:
                    point.X += du;
                    point.Y += dv;
                    break;
                case CadSketchPolygon polygon:
                    polygon.CenterX += du;
                    polygon.CenterY += dv;
                    break;
                case CadSketchSlot slot:
                    slot.Center1X += du;
                    slot.Center1Y += dv;
                    slot.Center2X += du;
                    slot.Center2Y += dv;
                    break;
                case CadSketchSpline spline:
                    for (var i = 0; i < spline.ControlPointsXY.Count; i += 2)
                    {
                        spline.ControlPointsXY[i] += du;
                        if (i + 1 < spline.ControlPointsXY.Count)
                        {
                            spline.ControlPointsXY[i + 1] += dv;
                        }
                    }
                    break;
            }
        }
    }

    private static void RotateSketchEntities(IReadOnlyList<CadSketchEntity> entities, double pivotU, double pivotV, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180d;
        var cosA = Math.Cos(rad);
        var sinA = Math.Sin(rad);

        static (double u, double v) RotatePt(double u, double v, double pu, double pv, double cos, double sin)
        {
            var du = u - pu;
            var dv = v - pv;
            return (pu + du * cos - dv * sin, pv + du * sin + dv * cos);
        }

        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CadSketchLine line:
                    (line.StartX, line.StartY) = RotatePt(line.StartX, line.StartY, pivotU, pivotV, cosA, sinA);
                    (line.EndX, line.EndY) = RotatePt(line.EndX, line.EndY, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchRectangle rect:
                    (rect.X, rect.Y) = RotatePt(rect.X, rect.Y, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchCircle circle:
                    (circle.CenterX, circle.CenterY) = RotatePt(circle.CenterX, circle.CenterY, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchArc arc:
                    (arc.CenterX, arc.CenterY) = RotatePt(arc.CenterX, arc.CenterY, pivotU, pivotV, cosA, sinA);
                    arc.StartAngleDegrees = NormalizeDegrees(arc.StartAngleDegrees + angleDeg);
                    arc.EndAngleDegrees = NormalizeDegrees(arc.EndAngleDegrees + angleDeg);
                    break;
                case CadSketchPoint point:
                    (point.X, point.Y) = RotatePt(point.X, point.Y, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchPolygon polygon:
                    (polygon.CenterX, polygon.CenterY) = RotatePt(polygon.CenterX, polygon.CenterY, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchSlot slot:
                    (slot.Center1X, slot.Center1Y) = RotatePt(slot.Center1X, slot.Center1Y, pivotU, pivotV, cosA, sinA);
                    (slot.Center2X, slot.Center2Y) = RotatePt(slot.Center2X, slot.Center2Y, pivotU, pivotV, cosA, sinA);
                    break;
                case CadSketchSpline spline:
                    for (var i = 0; i + 1 < spline.ControlPointsXY.Count; i += 2)
                    {
                        var (nu, nv) = RotatePt(spline.ControlPointsXY[i], spline.ControlPointsXY[i + 1], pivotU, pivotV, cosA, sinA);
                        spline.ControlPointsXY[i] = nu;
                        spline.ControlPointsXY[i + 1] = nv;
                    }
                    break;
            }
        }
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double ComputeArcSweep(double startDegrees, double endDegrees, bool counterClockwise)
    {
        var start = NormalizeDegrees(startDegrees);
        var end = NormalizeDegrees(endDegrees);
        var sweep = end - start;
        if (counterClockwise)
        {
            if (sweep < 0d)
            {
                sweep += 360d;
            }
        }
        else if (sweep > 0d)
        {
            sweep -= 360d;
        }

        return sweep;
    }

    private static double NormalizeDegrees(double value)
    {
        var normalized = value % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    public bool IsConstructionModeActive => _store.Project.ActiveSketchSession?.IsConstructionModeActive ?? false;

    public StudioWorkspaceActionResult ToggleConstructionMode()
    {
        var action = new CadCommandAction(CadCommandActionKind.ToggleConstructionMode);
        var result = _store.Apply(action);
        return PublishCadResultQuietly(result);
    }

    public StudioWorkspaceActionResult ApplySketchConstraint(string constraintName)
    {
        var action = new CadCommandAction(
            CadCommandActionKind.ApplySketchConstraint,
            EntityName: constraintName);
        var result = _store.Apply(action);
        return PublishCadResultQuietly(result);
    }

    public StudioWorkspaceActionResult DeleteSketchConstraint(int index)
    {
        var action = new CadCommandAction(
            CadCommandActionKind.DeleteSketchConstraint,
            Amount: index);
        var result = _store.Apply(action);
        return PublishCadResultQuietly(result);
    }

    public StudioWorkspaceActionResult EditSketch(Guid sketchId)
    {
        var action = new CadCommandAction(
            CadCommandActionKind.EditSketch,
            EntityId: sketchId);
        var result = _store.Apply(action);
        return PublishCadResult(result);
    }

    public StudioWorkspaceActionResult EditSketchEntityValue(Guid entityId, string key, double value)
    {
        var action = new CadCommandAction(
            CadCommandActionKind.EditSketchEntityValue,
            EntityId: entityId,
            EntityName: key,
            Amount: value);
        return PublishCadResult(_store.Apply(action));
    }

    public StudioWorkspaceActionResult DeleteSketchEntity(Guid entityId)
    {
        var action = new CadCommandAction(CadCommandActionKind.DeleteSketchEntity, EntityId: entityId);
        return PublishCadResult(_store.Apply(action));
    }

    public StudioWorkspaceActionResult SetSketchEntityConstruction(Guid entityId)
    {
        var action = new CadCommandAction(CadCommandActionKind.SetSketchEntityConstruction, EntityId: entityId);
        return PublishCadResult(_store.Apply(action));
    }

    public (string EntityType, IReadOnlyList<FormaCore.Engine.CadParameter> Parameters)? GetSketchEntityParameters(Guid entityId)
    {
        var session = _store.Project.ActiveSketchSession;
        if (session is null)
        {
            return null;
        }

        var entity = session.DraftEntities.FirstOrDefault(e => e.Id == entityId);
        if (entity is null)
        {
            return null;
        }

        return (entity.EntityType, entity.GetParameters());
    }

    public void ExportStl(string path)
    {
        var mesh = BuildExportMesh();
        STLExporter.Export(mesh, path);
    }

    public void ExportObj(string path)
    {
        var mesh = BuildExportMesh();
        OBJExporter.Export(mesh, path);
    }

    public void ExportStlBody(Guid bodyId, string path)
    {
        var mesh = BuildBodyMesh(bodyId);
        STLExporter.Export(mesh, path);
    }

    public void ExportObjBody(Guid bodyId, string path)
    {
        var mesh = BuildBodyMesh(bodyId);
        OBJExporter.Export(mesh, path);
    }

    private Mesh BuildBodyMesh(Guid bodyId)
    {
        var compile = _store.Compile();
        foreach (var body in compile.Bodies)
        {
            if (body.BodyId == bodyId)
                return _mesher.Tessellate(body.Solid);
        }
        throw new InvalidOperationException("Selected body not found in the current project.");
    }

    private Mesh BuildExportMesh()
    {
        var compile = _store.Compile();
        var combined = new Mesh();
        foreach (var body in compile.Bodies)
        {
            var bodyMesh = _mesher.Tessellate(body.Solid);
            var vertexOffset = combined.Vertices.Count;
            combined.Vertices.AddRange(bodyMesh.Vertices);
            foreach (var tri in bodyMesh.Triangles)
            {
                combined.Triangles.Add(new Triangle(
                    tri.A + vertexOffset,
                    tri.B + vertexOffset,
                    tri.C + vertexOffset));
            }
        }
        return combined;
    }

    private static bool Set<T>(out T target, T value)
    {
        target = value;
        return true;
    }
}

public sealed record StudioWorkspaceState(
    CadProject Project,
    CadCompileResult CompileResult,
    ViewportRenderState ViewportState,
    string CodeText,
    string LogText,
    string StatusMessage);

public sealed record StudioWorkspaceActionResult(
    bool Success,
    bool Mutated,
    string Message,
    bool RequestFocusSelection);
