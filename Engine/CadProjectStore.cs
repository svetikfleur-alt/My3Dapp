using System.Globalization;
using System.Text.Json;
using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class CadProjectStore
{
    private readonly CadProjectCompiler _compiler = new();

    public CadProjectStore()
        : this(CadProjectFactory.CreateDefault())
    {
    }

    public CadProjectStore(CadProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Project.Scene.EnsureReferencePlanes();
        if (Project.Selection.IsEmpty)
        {
            var topPlane = Project.Scene.ReferencePlanes.FirstOrDefault(plane => plane.Kind == CadReferencePlaneKind.Top);
            if (topPlane is not null)
            {
                Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, topPlane.Id, topPlane.Name);
            }
        }
    }

    public CadProject Project { get; }

    public CadCompileResult Compile()
    {
        return _compiler.Compile(Project);
    }

    public CadActionResult Apply(CadCommandAction action)
    {
        try
        {
            return action.Kind switch
            {
                CadCommandActionKind.CreatePrimitive => HandleCreatePrimitive(action),
                CadCommandActionKind.SelectPlane => HandleSelectPlane(action),
                CadCommandActionKind.SelectEntity => HandleSelectEntity(action),
                CadCommandActionKind.DeleteSelection => HandleDeleteSelection(),
                CadCommandActionKind.MoveSelected => HandleMoveSelected(action),
                CadCommandActionKind.StartSketch => HandleStartSketch(action),
                CadCommandActionKind.SetSketchTool => HandleSetSketchTool(action),
                CadCommandActionKind.PlaceSketchEntity => HandlePlaceSketchEntity(action),
                CadCommandActionKind.UpdateSketchPreview => HandleUpdateSketchPreview(action),
                CadCommandActionKind.ClearSketchPreview => HandleClearSketchPreview(),
                CadCommandActionKind.CancelSketchStep => HandleCancelSketchStep(),
                CadCommandActionKind.CancelSketch => HandleCancelSketch(),
                CadCommandActionKind.FinishSketch => HandleFinishSketch(),
                CadCommandActionKind.ApplySketchConstraint => HandleApplySketchConstraint(action),
                CadCommandActionKind.EditSketchEntityValue => HandleEditSketchEntityValue(action),
                CadCommandActionKind.DeleteSketchEntity => HandleDeleteSketchEntity(action),
                CadCommandActionKind.SetSketchEntityConstruction => HandleSetSketchEntityConstruction(action),
                CadCommandActionKind.ExtrudeSelectedSketch => HandleExtrudeSelectedSketch(action),
                CadCommandActionKind.RevolveSelectedSketch => HandleRevolveSelectedSketch(action),
                CadCommandActionKind.SweepSelectedSketch => HandleSweepSelectedSketch(action),
                CadCommandActionKind.LoftFromProfiles => HandleLoftFromProfiles(action),
                CadCommandActionKind.FilletSelectedBody => HandleFilletSelectedBody(action),
                CadCommandActionKind.ChamferSelectedBody => HandleChamferSelectedBody(action),
                CadCommandActionKind.ShellSelectedBody => HandleShellSelectedBody(action),
                CadCommandActionKind.MirrorSelectedBody => HandleMirrorSelectedBody(action),
                CadCommandActionKind.LinearPatternSelectedBody => HandleLinearPatternSelectedBody(action),
                CadCommandActionKind.CircularPatternSelectedBody => HandleCircularPatternSelectedBody(action),
                CadCommandActionKind.HoleSelectedBody => HandleHoleSelectedBody(action),
                CadCommandActionKind.FocusSelection => Success("Focus command acknowledged.", false),
                CadCommandActionKind.ToggleConstructionMode => HandleToggleConstructionMode(),
                CadCommandActionKind.BooleanUnion => HandleBooleanOperation(action, BooleanOperation.Union),
                CadCommandActionKind.BooleanSubtract => HandleBooleanOperation(action, BooleanOperation.Subtract),
                CadCommandActionKind.BooleanIntersect => HandleBooleanOperation(action, BooleanOperation.Intersect),
                CadCommandActionKind.EditSketch => HandleEditSketch(action),
                CadCommandActionKind.RenameBody => HandleRenameBody(action),
                CadCommandActionKind.DuplicateSelectedBody => HandleDuplicateSelectedBody(action),
                CadCommandActionKind.DeleteSketchConstraint => HandleDeleteSketchConstraint(action),
                CadCommandActionKind.SetBodyColor => HandleSetBodyColor(action),
                CadCommandActionKind.ToggleBodyVisibility => HandleToggleBodyVisibility(action),
                CadCommandActionKind.CreateDatumPlane => HandleCreateDatumPlane(action),
                CadCommandActionKind.ToggleDatumPlaneVisibility => HandleToggleDatumPlaneVisibility(action),
                CadCommandActionKind.DeleteDatumPlane => HandleDeleteDatumPlane(action),
                _ => Failure($"Unsupported CAD action: {action.Kind}.")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"CadProjectStore.Apply({action.Kind}) threw {ex.GetType().Name}: {ex}");
            return Failure($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public CadBody? FindSelectedBody()
    {
        return ResolveSelectedBody();
    }

    private CadActionResult HandleCreatePrimitive(CadCommandAction action)
    {
        if (action.PrimitiveKind is null)
        {
            return Failure("Primitive kind is required.");
        }

        var primitiveKind = action.PrimitiveKind.Value;
        var index = NextIndex(primitiveKind);
        var bodyName = $"{primitiveKind}{index}";
        var feature = CreatePrimitiveFeature(primitiveKind, bodyName);
        var body = new CadBody
        {
            Name = bodyName,
            Features =
            [
                feature
            ]
        };

        Project.Scene.Bodies.Add(body);
        Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);
        Project.ActiveMode = CadMode.DirectPrimitive;
        Project.ActiveSketchSession = null;

        return Success($"Created {body.Name}.", true);
    }

    private CadActionResult HandleSelectPlane(CadCommandAction action)
    {
        CadReferencePlane? plane = null;

        if (action.EntityId != Guid.Empty)
        {
            plane = Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Id == action.EntityId);
        }

        if (plane is null && action.PlaneKind is not null)
        {
            plane = Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Kind == action.PlaneKind.Value);
        }

        if (plane is null && !string.IsNullOrWhiteSpace(action.EntityName))
        {
            plane = Project.Scene.ReferencePlanes.FirstOrDefault(item =>
                item.Name.Equals(action.EntityName, StringComparison.OrdinalIgnoreCase));
        }

        if (plane is null)
        {
            return Failure("Reference plane not found.");
        }

        Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, plane.Id, plane.Name);
        return Success($"Selected plane {plane.Name}.", false);
    }

    private CadActionResult HandleSelectEntity(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty)
        {
            return Failure("Entity selection requires an ID.");
        }

        var plane = Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Id == action.EntityId);
        if (plane is not null)
        {
            Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, plane.Id, plane.Name);
            return Success($"Selected plane {plane.Name}.", false);
        }

        var body = Project.Scene.Bodies.FirstOrDefault(item => item.Id == action.EntityId);
        if (body is not null)
        {
            Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);
            return Success($"Selected body {body.Name}.", false);
        }

        foreach (var candidateBody in Project.Scene.Bodies)
        {
            var feature = candidateBody.Features.FirstOrDefault(item => item.Id == action.EntityId);
            if (feature is not null)
            {
                var kind = feature.Kind == CadFeatureKind.Sketch ? CadEntityKind.Sketch : CadEntityKind.Feature;
                Project.Selection = new CadSelection(kind, feature.Id, feature.Name);
                return Success($"Selected {feature.Name}.", false);
            }

            foreach (var sketch in candidateBody.Features.OfType<SketchFeature>())
            {
                var entity = sketch.Entities.FirstOrDefault(item => item.Id == action.EntityId);
                if (entity is null)
                {
                    continue;
                }

                Project.Selection = new CadSelection(CadEntityKind.SketchEntity, entity.Id, entity.EntityType);
                return Success($"Selected {entity.EntityType}.", false);
            }
        }

        return Failure("Entity not found.");
    }

    private CadActionResult HandleDeleteSelection()
    {
        if (Project.Selection.IsEmpty)
        {
            return Failure("Nothing is selected.");
        }

        if (Project.Selection.Kind == CadEntityKind.ReferencePlane)
        {
            return Failure("Reference planes cannot be deleted.");
        }

        if (Project.Selection.Kind == CadEntityKind.Body)
        {
            var body = Project.Scene.Bodies.FirstOrDefault(item => item.Id == Project.Selection.EntityId);
            if (body is null)
            {
                return Failure("Selected body no longer exists.");
            }

            Project.Scene.Bodies.Remove(body);
            Project.Selection = CadSelection.None;
            return Success($"Deleted {body.Name}.", true);
        }

        foreach (var body in Project.Scene.Bodies)
        {
            var featureIndex = body.Features.FindIndex(item => item.Id == Project.Selection.EntityId);
            if (featureIndex < 0)
            {
                continue;
            }

            var removedFeatures = body.Features.Skip(featureIndex).ToList();
            body.Features.RemoveRange(featureIndex, body.Features.Count - featureIndex);

            if (body.Features.Count == 0)
            {
                Project.Scene.Bodies.Remove(body);
                Project.Selection = CadSelection.None;
                return Success($"Deleted {removedFeatures.Count.ToString(CultureInfo.InvariantCulture)} feature(s) from {body.Name}.", true);
            }

            Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);
            return Success($"Deleted {removedFeatures.Count.ToString(CultureInfo.InvariantCulture)} feature(s) from {body.Name}.", true);
        }

        return Failure("Selected entity no longer exists.");
    }

    private CadActionResult HandleMoveSelected(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null)
        {
            return Failure("Select a body before moving it.");
        }

        var distance = action.Amount;
        if (!double.IsFinite(distance))
        {
            return Failure("Move distance is invalid.");
        }

        var feature = new MoveFeature
        {
            Name = $"Move{body.Features.Count(item => item is MoveFeature) + 1}"
        };

        switch (action.Axis)
        {
            case CadAxis.X:
                feature.X = distance;
                break;
            case CadAxis.Y:
                feature.Y = distance;
                break;
            case CadAxis.Z:
                feature.Z = distance;
                break;
        }

        body.Features.Add(feature);
        Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);
        return Success($"Moved {body.Name} by {distance.ToString("0.###", CultureInfo.InvariantCulture)} along {action.Axis}.", true);
    }

    private CadActionResult HandleStartSketch(CadCommandAction action)
    {
        // Face-plane sketch: use the transient SelectedFacePlane if set
        if (action.PlaneKind is null && string.IsNullOrWhiteSpace(action.EntityName)
            && Project.SelectedFacePlane is { } fp)
        {
            var facePlaneId = Guid.NewGuid();
            Project.ActiveMode = CadMode.Sketch;
            Project.ActiveSketchSession = new CadSketchSession
            {
                PlaneId = facePlaneId,
                PlaneName = "Face",
                FacePlane = fp,
                ActiveTool = action.SketchTool ?? CadSketchToolKind.Rectangle
            };
            Project.SelectedFacePlane = null;
            return Success("Started sketch on face. Draw a closed profile, then extrude.", false);
        }

        var planeResult = action.PlaneKind is null && string.IsNullOrWhiteSpace(action.EntityName)
            ? ResolveSelectedPlane()
            : ResolvePlane(action);

        if (planeResult is null)
        {
            return Failure("Select Top, Front, or Right before starting a sketch.");
        }

        Project.ActiveMode = CadMode.Sketch;
        Project.ActiveSketchSession = new CadSketchSession
        {
            PlaneId = planeResult.Id,
            PlaneName = planeResult.Name,
            ActiveTool = action.SketchTool ?? CadSketchToolKind.Rectangle
        };
        Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, planeResult.Id, planeResult.Name);

        return Success(
            $"Started sketch on {planeResult.Name}. Choose Point, Line, Rectangle, Circle, or Arc to add geometry.",
            false);
    }

    private CadActionResult HandleSetSketchTool(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("Start a sketch before choosing line, rectangle, circle, arc, or point.");
        }

        var tool = action.SketchTool ?? CadSketchToolKind.Rectangle;
        Project.ActiveSketchSession.ActiveTool = tool;
        Project.ActiveSketchSession.PendingLineStart = null;
        Project.ActiveSketchSession.PendingShapeAnchor = null;
        Project.ActiveSketchSession.PendingSketchPoints.Clear();
        Project.ActiveSketchSession.PreviewEntities.Clear();
        Project.ActiveSketchSession.PendingAngleDimFirstId = null;

        if (action.Amount > 0)
        {
            switch (tool)
            {
                case CadSketchToolKind.Polygon:
                    Project.ActiveSketchSession.PendingPolygonSides = Math.Clamp((int)Math.Round(action.Amount), 3, 50);
                    break;
                case CadSketchToolKind.Offset:
                    Project.ActiveSketchSession.PendingOffsetDistance = action.Amount;
                    break;
                case CadSketchToolKind.Fillet2d:
                    Project.ActiveSketchSession.PendingFilletRadius = action.Amount;
                    break;
            }
        }

        return Success($"Sketch tool set to {tool}. Click on the active plane to place geometry.", false);
    }

    private CadActionResult HandleUpdateSketchPreview(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("Start a sketch before previewing geometry.");
        }

        var session = Project.ActiveSketchSession;
        session.PreviewEntities = BuildPreviewEntities(session, new Vector2D(action.U, action.V));
        return Success("Sketch preview updated.", false);
    }

    private CadActionResult HandleClearSketchPreview()
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        Project.ActiveSketchSession.PreviewEntities.Clear();
        return Success("Sketch preview cleared.", false);
    }

    private CadActionResult HandleCancelSketchStep()
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var session = Project.ActiveSketchSession;

        if (session.ActiveTool == CadSketchToolKind.Spline && session.PendingSketchPoints.Count >= 2)
        {
            var spline = new CadSketchSpline();
            foreach (var p in session.PendingSketchPoints)
            {
                spline.ControlPointsXY.Add(p.X);
                spline.ControlPointsXY.Add(p.Y);
            }

            session.DraftEntities.Add(spline);
            session.PendingSketchPoints.Clear();
            session.PreviewEntities.Clear();
            return Success($"Committed spline with {spline.ControlPointsXY.Count / 2} control points.", false);
        }

        session.PendingLineStart = null;
        session.PendingShapeAnchor = null;
        session.PendingSketchPoints.Clear();
        session.PreviewEntities.Clear();
        session.PendingAngleDimFirstId = null;
        return Success("Sketch step cancelled.", false);
    }

    private CadActionResult HandleApplySketchConstraint(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var session = Project.ActiveSketchSession;
        var constraintName = action.EntityName?.Trim() ?? string.Empty;

        return constraintName.ToLowerInvariant() switch
        {
            "horizontal" => ApplyLineOrientationConstraint(session, CadSketchConstraintKind.Horizontal),
            "vertical" => ApplyLineOrientationConstraint(session, CadSketchConstraintKind.Vertical),
            "coincident" => ApplyCoincidentConstraint(session),
            "equal" => ApplyEqualConstraint(session),
            "fix" or "fixed" => ApplyFixedConstraint(session),
            "tangent" => ApplyTangentConstraint(session),
            "parallel" => ApplyParallelConstraint(session),
            "perpendicular" => ApplyPerpendicularConstraint(session),
            "concentric" => ApplyConcentricConstraint(session),
            _ => Failure($"Unknown constraint: {constraintName}.")
        };
    }

    private CadActionResult ApplyLineOrientationConstraint(CadSketchSession session, CadSketchConstraintKind kind)
    {
        var lastLine = session.DraftEntities.OfType<CadSketchLine>().LastOrDefault();
        if (lastLine is null)
        {
            return Failure("No line entity to constrain. Draw a line first.");
        }

        if (lastLine.IsFixed)
        {
            return Failure("Last line is fixed. Remove the fixed constraint before changing its orientation.");
        }

        if (kind == CadSketchConstraintKind.Horizontal)
        {
            lastLine.EndY = lastLine.StartY;
            AddManualConstraint(session, new CadSketchConstraint(
                CadSketchConstraintKind.Horizontal,
                "Line is constrained horizontal.",
                [lastLine.Id]));
            SolveSketchSession(session);
            session.PreviewEntities.Clear();
            return Success("Applied horizontal constraint to last line.", false);
        }

        lastLine.EndX = lastLine.StartX;
        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Vertical,
            "Line is constrained vertical.",
            [lastLine.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied vertical constraint to last line.", false);
    }

    private CadActionResult ApplyCoincidentConstraint(CadSketchSession session)
    {
        var lines = session.DraftEntities.OfType<CadSketchLine>().ToList();
        if (lines.Count < 2)
        {
            return Failure("Coincident needs two line entities. Draw two connected lines first.");
        }

        var previous = lines[^2];
        var current = lines[^1];
        if (current.IsFixed)
        {
            return Failure("Last line is fixed. Remove the fixed constraint before making it coincident.");
        }

        current.StartX = previous.EndX;
        current.StartY = previous.EndY;

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Coincident,
            "Last line start is coincident with previous line end.",
            [previous.Id, current.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied coincident constraint between the last two lines.", false);
    }

    private CadActionResult ApplyEqualConstraint(CadSketchSession session)
    {
        var geometry = session.DraftEntities
            .Where(entity => entity is CadSketchLine or CadSketchCircle or CadSketchArc)
            .ToList();
        if (geometry.Count < 2)
        {
            return Failure("Equal needs two sketch entities. Draw two lines, circles, or arcs first.");
        }

        var reference = geometry[^2];
        var driven = geometry[^1];
        if (driven.IsFixed)
        {
            return Failure($"{driven.EntityType} is fixed. Remove the fixed constraint before applying equal.");
        }

        switch (reference, driven)
        {
            case (CadSketchLine a, CadSketchLine b):
            {
                var targetLength = LineLength(a);
                var currentLength = LineLength(b);
                if (targetLength <= 0.001d || currentLength <= 0.001d)
                {
                    return Failure("Equal length needs two non-zero lines.");
                }

                var ux = (b.EndX - b.StartX) / currentLength;
                var uy = (b.EndY - b.StartY) / currentLength;
                b.EndX = b.StartX + ux * targetLength;
                b.EndY = b.StartY + uy * targetLength;
                AddManualConstraint(session, new CadSketchConstraint(
                    CadSketchConstraintKind.EqualLength,
                    "Last line length equals previous line length.",
                    [a.Id, b.Id]));
                SolveSketchSession(session);
                session.PreviewEntities.Clear();
                return Success("Applied equal length between the last two lines.", false);
            }

            case (CadSketchCircle a, CadSketchCircle b):
                b.Radius = a.Radius;
                break;
            case (CadSketchCircle a, CadSketchArc b):
                b.Radius = a.Radius;
                break;
            case (CadSketchArc a, CadSketchCircle b):
                b.Radius = a.Radius;
                break;
            case (CadSketchArc a, CadSketchArc b):
                b.Radius = a.Radius;
                break;
            default:
                return Failure("Equal currently supports line length or circle/arc radius.");
        }

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.EqualRadius,
            "Last radius equals previous radius.",
            [reference.Id, driven.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied equal radius between the last two radius entities.", false);
    }

    private CadActionResult ApplyFixedConstraint(CadSketchSession session)
    {
        var entity = session.DraftEntities.LastOrDefault();
        if (entity is null)
        {
            return Failure("Fix needs a sketch entity. Draw or select an entity first.");
        }

        entity.IsFixed = true;
        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Fixed,
            $"{entity.EntityType} fixed in place.",
            [entity.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success($"Fixed {entity.EntityType} in place.", false);
    }

    private CadActionResult ApplyParallelConstraint(CadSketchSession session)
    {
        var lines = session.DraftEntities.OfType<CadSketchLine>().ToList();
        if (lines.Count < 2)
            return Failure("Parallel needs two line entities. Draw two lines first.");

        var reference = lines[^2];
        var driven = lines[^1];
        if (driven.IsFixed)
            return Failure("Last line is fixed. Remove the fixed constraint before applying parallel.");

        var refLen = LineLength(reference);
        var drivenLen = LineLength(driven);
        if (refLen < 0.001d || drivenLen < 0.001d)
            return Failure("Parallel needs two non-zero lines.");

        var ux = (reference.EndX - reference.StartX) / refLen;
        var uy = (reference.EndY - reference.StartY) / refLen;
        driven.EndX = driven.StartX + ux * drivenLen;
        driven.EndY = driven.StartY + uy * drivenLen;

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Parallel,
            "Lines are constrained parallel.",
            [reference.Id, driven.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied parallel constraint to last two lines.", false);
    }

    private CadActionResult ApplyPerpendicularConstraint(CadSketchSession session)
    {
        var lines = session.DraftEntities.OfType<CadSketchLine>().ToList();
        if (lines.Count < 2)
            return Failure("Perpendicular needs two line entities. Draw two lines first.");

        var reference = lines[^2];
        var driven = lines[^1];
        if (driven.IsFixed)
            return Failure("Last line is fixed. Remove the fixed constraint before applying perpendicular.");

        var refLen = LineLength(reference);
        var drivenLen = LineLength(driven);
        if (refLen < 0.001d || drivenLen < 0.001d)
            return Failure("Perpendicular needs two non-zero lines.");

        var ux = (reference.EndX - reference.StartX) / refLen;
        var uy = (reference.EndY - reference.StartY) / refLen;
        driven.EndX = driven.StartX + (-uy) * drivenLen;
        driven.EndY = driven.StartY + ux * drivenLen;

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Perpendicular,
            "Lines are constrained perpendicular.",
            [reference.Id, driven.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied perpendicular constraint to last two lines.", false);
    }

    private CadActionResult ApplyConcentricConstraint(CadSketchSession session)
    {
        var circles = session.DraftEntities
            .Where(e => e is CadSketchCircle or CadSketchArc)
            .ToList();
        if (circles.Count < 2)
            return Failure("Concentric needs two circles or arcs. Draw two circles/arcs first.");

        var reference = circles[^2];
        var driven = circles[^1];
        if (driven.IsFixed)
            return Failure($"{driven.EntityType} is fixed. Remove the fixed constraint before applying concentric.");

        double refCx, refCy;
        switch (reference)
        {
            case CadSketchCircle c: refCx = c.CenterX; refCy = c.CenterY; break;
            case CadSketchArc a: refCx = a.CenterX; refCy = a.CenterY; break;
            default: return Failure("Concentric needs circles or arcs.");
        }

        switch (driven)
        {
            case CadSketchCircle c: c.CenterX = refCx; c.CenterY = refCy; break;
            case CadSketchArc a: a.CenterX = refCx; a.CenterY = refCy; break;
        }

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Concentric,
            "Entities share the same center.",
            [reference.Id, driven.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied concentric constraint to last two circles/arcs.", false);
    }

    private CadActionResult ApplyTangentConstraint(CadSketchSession session)
    {
        var entities = session.DraftEntities
            .Where(e => e is CadSketchLine or CadSketchArc or CadSketchCircle)
            .ToList();
        if (entities.Count < 2)
            return Failure("Tangent needs a line and an arc (or two arcs). Draw the entities first.");

        var last = entities[^1];
        var prev = entities[^2];

        var valid = (last is CadSketchLine && prev is CadSketchArc) ||
                    (last is CadSketchArc && prev is CadSketchLine) ||
                    (last is CadSketchArc && prev is CadSketchArc);
        if (!valid)
            return Failure("Tangent requires a line and an arc sharing a point. Not applicable to two lines.");

        if (last.IsFixed)
            return Failure($"{last.EntityType} is fixed. Remove the fixed constraint before applying tangent.");

        var line = (last as CadSketchLine) ?? (prev as CadSketchLine);
        var arc = (last as CadSketchArc) ?? (prev as CadSketchArc);

        if (line is not null && arc is not null)
        {
            var arcStartX = arc.CenterX + arc.Radius * Math.Cos(arc.StartAngleDegrees * Math.PI / 180d);
            var arcStartY = arc.CenterY + arc.Radius * Math.Sin(arc.StartAngleDegrees * Math.PI / 180d);
            var arcEndX = arc.CenterX + arc.Radius * Math.Cos(arc.EndAngleDegrees * Math.PI / 180d);
            var arcEndY = arc.CenterY + arc.Radius * Math.Sin(arc.EndAngleDegrees * Math.PI / 180d);

            var linePts = new[] { (line.StartX, line.StartY), (line.EndX, line.EndY) };
            var arcPts = new[] { (arcStartX, arcStartY), (arcEndX, arcEndY) };

            var minDist = double.MaxValue;
            var bestLineX = line.StartX;
            var bestLineY = line.StartY;
            var bestArcX = arcStartX;
            var bestArcY = arcStartY;

            foreach (var (lx, ly) in linePts)
            {
                foreach (var (ax, ay) in arcPts)
                {
                    var dist = Math.Sqrt(Math.Pow(lx - ax, 2) + Math.Pow(ly - ay, 2));
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestLineX = lx; bestLineY = ly;
                        bestArcX = ax; bestArcY = ay;
                    }
                }
            }

            arc.CenterX += bestLineX - bestArcX;
            arc.CenterY += bestLineY - bestArcY;
        }
        else if (last is CadSketchArc arc1 && prev is CadSketchArc arc2)
        {
            var a2EndX = arc2.CenterX + arc2.Radius * Math.Cos(arc2.EndAngleDegrees * Math.PI / 180d);
            var a2EndY = arc2.CenterY + arc2.Radius * Math.Sin(arc2.EndAngleDegrees * Math.PI / 180d);
            var a1StartX = arc1.CenterX + arc1.Radius * Math.Cos(arc1.StartAngleDegrees * Math.PI / 180d);
            var a1StartY = arc1.CenterY + arc1.Radius * Math.Sin(arc1.StartAngleDegrees * Math.PI / 180d);
            arc1.CenterX += a2EndX - a1StartX;
            arc1.CenterY += a2EndY - a1StartY;
        }

        AddManualConstraint(session, new CadSketchConstraint(
            CadSketchConstraintKind.Tangent,
            "Entities are tangent at their shared point.",
            [prev.Id, last.Id]));
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success("Applied tangent constraint.", false);
    }

    private CadActionResult HandleEditSketchEntityValue(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty || string.IsNullOrWhiteSpace(action.EntityName))
        {
            return Failure("Entity ID and parameter key are required.");
        }

        var key = action.EntityName!;
        var value = action.Amount;

        if (string.Equals(key, "Angle", StringComparison.OrdinalIgnoreCase))
        {
            return HandleEditAngleDimension(action.EntityId, value);
        }

        if (Project.ActiveSketchSession is not null)
        {
            var entity = Project.ActiveSketchSession.DraftEntities.FirstOrDefault(e => e.Id == action.EntityId);
            if (entity is not null)
            {
                if (entity.IsFixed)
                {
                    return Failure($"{entity.EntityType} is fixed. Remove the fixed constraint before editing.");
                }

                if (!entity.TrySetParameter(key, value))
                {
                    return Failure($"Cannot set {key} on this entity.");
                }

                SolveSketchSession(Project.ActiveSketchSession);
                Project.ActiveSketchSession.PreviewEntities.Clear();
                return Success($"Updated {key} to {value:0.###}.", true);
            }
        }

        foreach (var body in Project.Scene.Bodies)
        {
            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                var entity = sketch.Entities.FirstOrDefault(e => e.Id == action.EntityId);
                if (entity is null)
                {
                    continue;
                }

                if (entity.IsFixed)
                {
                    return Failure($"{entity.EntityType} is fixed. Remove the fixed constraint before editing dimensions.");
                }

                if (!entity.TrySetParameter(key, value))
                {
                    return Failure($"Cannot set {key} on this entity.");
                }

                sketch.Dimensions = InferDimensions(sketch.Entities);
                sketch.Constraints = InferConstraints(sketch.Entities);
                return Success($"Updated {key} to {value:0.###}.", true);
            }
        }

        return Failure("Sketch entity not found.");
    }

    private CadActionResult HandleEditAngleDimension(Guid entityId, double newAngleDeg)
    {
        newAngleDeg = Math.Clamp(newAngleDeg, 0.1, 179.9);

        foreach (var body in Project.Scene.Bodies)
        {
            foreach (var sketch in body.Features.OfType<SketchFeature>())
            {
                var dim = sketch.Dimensions.FirstOrDefault(d =>
                    d.Kind == CadSketchDimensionKind.Angle && d.EntityIds.Contains(entityId));
                if (dim is null)
                {
                    continue;
                }

                var lineAId = dim.EntityIds[0];
                var lineBId = dim.EntityIds[1];
                var lineA = sketch.Entities.OfType<CadSketchLine>().FirstOrDefault(l => l.Id == lineAId);
                var lineB = sketch.Entities.OfType<CadSketchLine>().FirstOrDefault(l => l.Id == lineBId);
                if (lineA is null || lineB is null)
                {
                    return Failure("One or both lines of the angle dimension were not found.");
                }

                if (lineA.IsFixed || lineB.IsFixed)
                {
                    return Failure("One or both dimensioned lines are fixed. Remove the fixed constraint before editing the angle.");
                }

                if (!TryLineIntersection2D(lineA, lineB, out var ix, out var iy))
                {
                    return Failure("Lines are parallel; cannot adjust angle.");
                }

                var newAngleRad = newAngleDeg * Math.PI / 180.0;
                var dx1 = lineA.EndX - lineA.StartX;
                var dy1 = lineA.EndY - lineA.StartY;
                var baseAngle = Math.Atan2(dy1, dx1);
                var targetAngle = baseAngle + newAngleRad;

                var len2 = Math.Sqrt(
                    (lineB.EndX - lineB.StartX) * (lineB.EndX - lineB.StartX) +
                    (lineB.EndY - lineB.StartY) * (lineB.EndY - lineB.StartY));

                lineB.StartX = ix;
                lineB.StartY = iy;
                lineB.EndX = ix + Math.Cos(targetAngle) * len2;
                lineB.EndY = iy + Math.Sin(targetAngle) * len2;

                var updatedLabel = $"{newAngleDeg:0.#}°";
                sketch.Dimensions = sketch.Dimensions
                    .Select(d => d == dim
                        ? new CadSketchDimension(d.Kind, updatedLabel, newAngleDeg, d.EntityIds, d.ParameterKey)
                        : d)
                    .ToList();

                return Success($"Angle updated to {updatedLabel}.", true);
            }
        }

        return Failure("Angle dimension not found.");
    }

    private static bool TryLineIntersection2D(CadSketchLine a, CadSketchLine b, out double ix, out double iy)
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

    private CadActionResult HandlePlaceSketchEntity(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("Start a sketch before placing geometry.");
        }

        var session = Project.ActiveSketchSession;
        var point = new Vector2D(action.U, action.V);
        string? toolMessage = null;
        IReadOnlyList<CadSketchEntity>? entities = session.ActiveTool switch
        {
            CadSketchToolKind.Line => CreatePlacedLine(session, point, out toolMessage) is { } line
                ? [line]
                : null,
            CadSketchToolKind.Circle => CreatePlacedCircle(session, point, out toolMessage) is { } circle
                ? [circle]
                : null,
            CadSketchToolKind.Arc => CreatePlacedArc(session, point, out toolMessage) is { } arc
                ? [arc]
                : null,
            CadSketchToolKind.Point => [CreatePlacedPoint(point)],
            CadSketchToolKind.Polygon => CreatePlacedPolygon(session, point, out toolMessage) is { } polygon
                ? [polygon]
                : null,
            CadSketchToolKind.Slot => CreatePlacedSlot(session, point, out toolMessage) is { } slot
                ? [slot]
                : null,
            CadSketchToolKind.Spline => CreateOrExtendSpline(session, point, out toolMessage),
            CadSketchToolKind.Offset => CreatePlacedOffset(session, point, out toolMessage),
            CadSketchToolKind.Fillet2d => CreatePlacedFillet2d(session, point, out toolMessage),
            CadSketchToolKind.Mirror => CreatePlacedMirror(session, point, out toolMessage),
            CadSketchToolKind.Trim => CreatePlacedTrim(session, point, out toolMessage),
            CadSketchToolKind.AngleDimension => HandleAngleDimensionStep(session, point, out toolMessage),
            CadSketchToolKind.LinearDimension => HandleLinearDimensionStep(session, point, out toolMessage),
            CadSketchToolKind.RadiusDimension => HandleRadiusDimensionStep(session, point, out toolMessage),
            _ => CreatePlacedRectangleLines(session, point, out toolMessage)
        };

        if (entities is null || entities.Count == 0)
        {
            session.PreviewEntities = BuildPreviewEntities(session, point);
            return Success(toolMessage ?? "Sketch point placed.", false);
        }

        if (session.IsConstructionModeActive)
        {
            foreach (var entity in entities)
            {
                entity.IsConstruction = true;
            }
        }

        session.DraftEntities.AddRange(entities);
        session.PreviewEntities.Clear();
        var message = session.ActiveTool switch
        {
            CadSketchToolKind.Line => toolMessage ?? "Added line segment.",
            CadSketchToolKind.Circle => toolMessage ?? "Added circle.",
            CadSketchToolKind.Arc => toolMessage ?? "Added arc.",
            CadSketchToolKind.Point => "Added point.",
            CadSketchToolKind.Polygon => toolMessage ?? "Added polygon.",
            CadSketchToolKind.Slot => toolMessage ?? "Added slot.",
            CadSketchToolKind.Spline => toolMessage ?? "Added spline control point.",
            CadSketchToolKind.Offset => toolMessage ?? "Added offset.",
            CadSketchToolKind.Fillet2d => toolMessage ?? "Added fillet.",
            CadSketchToolKind.Mirror => toolMessage ?? "Mirror applied.",
            CadSketchToolKind.Trim => toolMessage ?? "Trim applied.",
            CadSketchToolKind.AngleDimension => toolMessage ?? "Angle dimension placed.",
            CadSketchToolKind.LinearDimension => toolMessage ?? "Linear dimension placed.",
            CadSketchToolKind.RadiusDimension => toolMessage ?? "Radius dimension placed.",
            _ => toolMessage ?? "Added rectangle as connected lines."
        };
        return Success(message, false);
    }

    private CadActionResult HandleToggleConstructionMode()
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("Start a sketch before toggling construction mode.");
        }

        var session = Project.ActiveSketchSession;
        session.IsConstructionModeActive = !session.IsConstructionModeActive;
        var state = session.IsConstructionModeActive ? "on" : "off";
        return Success($"Construction mode {state}.", false);
    }

    private CadActionResult HandleDeleteSketchEntity(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty)
        {
            return Failure("Entity ID is required.");
        }

        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var session = Project.ActiveSketchSession;
        var entity = session.DraftEntities.FirstOrDefault(e => e.Id == action.EntityId);
        if (entity is null)
        {
            return Failure("Entity not found in active sketch.");
        }

        session.DraftEntities.Remove(entity);

        session.ManualConstraints = session.ManualConstraints
            .Where(c => !c.EntityIds.Contains(action.EntityId))
            .ToList();

        session.ManualDimensions = session.ManualDimensions
            .Where(d => !d.EntityIds.Contains(action.EntityId))
            .ToList();

        session.PreviewEntities.Clear();
        return Success("Entity deleted from sketch.", true);
    }

    private CadActionResult HandleDeleteSketchConstraint(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var session = Project.ActiveSketchSession;
        var index = (int)Math.Round(action.Amount);

        if (index < 0 || index >= session.ManualConstraints.Count)
        {
            return Failure($"Constraint index {index} out of range.");
        }

        session.ManualConstraints.RemoveAt(index);
        SolveSketchSession(session);
        return Success("Constraint removed.", true);
    }

    private CadActionResult HandleSetSketchEntityConstruction(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty)
        {
            return Failure("Entity ID is required.");
        }

        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var session = Project.ActiveSketchSession;
        var entity = session.DraftEntities.FirstOrDefault(e => e.Id == action.EntityId);
        if (entity is null)
        {
            return Failure("Entity not found in active sketch.");
        }

        entity.IsConstruction = !entity.IsConstruction;
        var state = entity.IsConstruction ? "construction" : "normal";
        SolveSketchSession(session);
        session.PreviewEntities.Clear();
        return Success($"Entity set to {state} mode.", true);
    }

    private CadActionResult HandleCancelSketch()
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        var planeName = Project.ActiveSketchSession.PlaneName;
        Project.ActiveSketchSession = null;
        Project.ActiveMode = CadMode.DirectPrimitive;
        return Success($"Canceled sketch on {planeName}.", false);
    }

    private CadActionResult HandleFinishSketch()
    {
        if (Project.ActiveSketchSession is null)
        {
            return Failure("No active sketch session.");
        }

        if (Project.ActiveSketchSession.DraftEntities.Count == 0)
        {
            Project.ActiveSketchSession = null;
            Project.ActiveMode = CadMode.DirectPrimitive;
            return Failure("Sketch contains no entities yet.");
        }

        var session = Project.ActiveSketchSession;
        var normalizedEntities = NormalizeSketchEntities(session.DraftEntities);

        if (session.EditingSketchId != Guid.Empty)
        {
            // Edit-session path: update the existing sketch in-place.
            var existingSketch = FindSketchById(session.EditingSketchId);
            if (existingSketch is null)
            {
                Project.ActiveSketchSession = null;
                Project.ActiveMode = CadMode.DirectPrimitive;
                return Failure("Edited sketch was not found — session aborted.");
            }

            existingSketch.Entities = normalizedEntities;
            existingSketch.Constraints = MergeSketchConstraints(
                InferConstraints(normalizedEntities),
                session.ManualConstraints);
            existingSketch.Dimensions = MergeSketchDimensions(
                InferDimensions(normalizedEntities),
                session.ManualDimensions);

            Project.ActiveSketchSession = null;
            Project.ActiveMode = CadMode.DirectPrimitive;
            Project.Selection = new CadSelection(CadEntityKind.Sketch, existingSketch.Id, existingSketch.Name);
            return Success(
                existingSketch.IsClosedProfile
                    ? $"Updated {existingSketch.Name}."
                    : $"Updated {existingSketch.Name} (open sketch).",
                true);
        }

        var sketchIndex = NextFeatureIndex(CadFeatureKind.Sketch);
        var sketch = new SketchFeature
        {
            Name = $"Sketch{sketchIndex}",
            PlaneId = session.PlaneId,
            PlaneName = session.PlaneName,
            FacePlane = session.FacePlane,
            Entities = normalizedEntities,
            Constraints = MergeSketchConstraints(
                InferConstraints(normalizedEntities),
                session.ManualConstraints)
        };
        sketch.Dimensions = MergeSketchDimensions(
            InferDimensions(normalizedEntities),
            session.ManualDimensions);

        var sketchBody = new CadBody
        {
            Name = sketch.Name,
            Features =
            [
                sketch
            ]
        };

        Project.Scene.Bodies.Add(sketchBody);
        Project.ActiveSketchSession = null;
        Project.ActiveMode = CadMode.DirectPrimitive;
        Project.Selection = new CadSelection(CadEntityKind.Sketch, sketch.Id, sketch.Name);
        return Success(
            sketch.IsClosedProfile
                ? $"Committed {sketch.Name}."
                : $"Committed {sketch.Name} as an open sketch. Open sketches cannot extrude yet.",
            true);
    }

    private CadActionResult HandleEditSketch(CadCommandAction action)
    {
        if (Project.ActiveSketchSession is not null)
            return Failure("Finish the current sketch first.");

        var sketchId = action.EntityId;
        var sketch = FindSketchById(sketchId);
        if (sketch is null)
            return Failure("Sketch not found.");

        // Deep-copy entities so Cancel leaves the original untouched.
        List<CadSketchEntity> draftCopy;
        try
        {
            var json = JsonSerializer.Serialize(sketch.Entities);
            draftCopy = JsonSerializer.Deserialize<List<CadSketchEntity>>(json) ?? [];
        }
        catch
        {
            draftCopy = [];
        }

        Project.ActiveMode = CadMode.Sketch;
        Project.ActiveSketchSession = new CadSketchSession
        {
            PlaneId = sketch.PlaneId,
            PlaneName = sketch.PlaneName,
            EditingSketchId = sketch.Id,
            DraftEntities = draftCopy,
            ManualConstraints = sketch.Constraints.ToList(),
            ManualDimensions = sketch.Dimensions.ToList()
        };
        Project.Selection = new CadSelection(CadEntityKind.Sketch, sketch.Id, sketch.Name);
        return Success($"Editing {sketch.Name}.", true);
    }

    private CadActionResult HandleRenameBody(CadCommandAction action)
    {
        var newName = action.EntityName?.Trim();
        if (string.IsNullOrWhiteSpace(newName))
            return Failure("New name is required.");

        var body = action.EntityId != Guid.Empty
            ? Project.Scene.Bodies.FirstOrDefault(b => b.Id == action.EntityId)
            : ResolveSelectedBody();

        if (body is null)
            return Failure("Select a body to rename.");

        var old = body.Name;
        body.Name = newName;
        if (Project.Selection.EntityId == body.Id)
            Project.Selection = new CadSelection(Project.Selection.Kind, body.Id, newName);

        return Success($"Renamed '{old}' to '{newName}'.", true);
    }

    private CadActionResult HandleSetBodyColor(CadCommandAction action)
    {
        var body = action.EntityId != Guid.Empty
            ? Project.Scene.Bodies.FirstOrDefault(b => b.Id == action.EntityId)
            : ResolveSelectedBody();

        if (body is null)
            return Failure("Select a body to set color.");

        // EntityName holds the color string (e.g. "#4080c0") or empty to reset
        var color = action.EntityName?.Trim();
        body.Color = string.IsNullOrEmpty(color) ? null : color;
        return Success(body.Color is null
            ? $"Reset color of '{body.Name}' to default."
            : $"Set color of '{body.Name}' to {body.Color}.", true);
    }

    private CadActionResult HandleToggleBodyVisibility(CadCommandAction action)
    {
        var body = action.EntityId != Guid.Empty
            ? Project.Scene.Bodies.FirstOrDefault(b => b.Id == action.EntityId)
            : ResolveSelectedBody();

        if (body is null)
            return Failure("Select a body to toggle visibility.");

        body.Visible = !body.Visible;
        return Success(body.Visible
            ? $"Showed '{body.Name}'."
            : $"Hidden '{body.Name}'.", true);
    }

    private CadActionResult HandleCreateDatumPlane(CadCommandAction action)
    {
        if (action.PlaneKind is null)
            return Failure("Datum plane requires a source plane kind.");

        var offset = action.Amount;
        var name = string.IsNullOrWhiteSpace(action.EntityName)
            ? $"Datum{Project.Scene.ReferencePlanes.Count(p => p.Kind == CadReferencePlaneKind.Datum) + 1}"
            : action.EntityName.Trim();

        var plane = new CadReferencePlane
        {
            Name = name,
            Kind = CadReferencePlaneKind.Datum,
            DatumSourceKind = action.PlaneKind.Value,
            DatumOffsetDistance = offset,
            Visible = true
        };

        Project.Scene.ReferencePlanes.Add(plane);
        Project.Selection = new CadSelection(CadEntityKind.ReferencePlane, plane.Id, plane.Name);
        return Success($"Created datum plane '{plane.Name}' offset {offset:0.###} mm from {action.PlaneKind.Value}.", true);
    }

    private CadActionResult HandleToggleDatumPlaneVisibility(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty)
            return Failure("Datum plane ID required.");

        var plane = Project.Scene.ReferencePlanes.FirstOrDefault(p => p.Id == action.EntityId);
        if (plane is null)
            return Failure("Datum plane not found.");

        plane.Visible = !plane.Visible;
        return Success(plane.Visible ? $"Showed '{plane.Name}'." : $"Hidden '{plane.Name}'.", true);
    }

    private CadActionResult HandleDeleteDatumPlane(CadCommandAction action)
    {
        if (action.EntityId == Guid.Empty)
            return Failure("Datum plane ID required.");

        var plane = Project.Scene.ReferencePlanes.FirstOrDefault(p => p.Id == action.EntityId
            && p.Kind == CadReferencePlaneKind.Datum);
        if (plane is null)
            return Failure("Datum plane not found or cannot delete a reference plane.");

        Project.Scene.ReferencePlanes.Remove(plane);
        if (Project.Selection.EntityId == plane.Id)
            Project.Selection = CadSelection.None;
        return Success($"Deleted datum plane '{plane.Name}'.", true);
    }

    private CadActionResult HandleDuplicateSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null)
            return Failure("Select a body to duplicate.");

        string json;
        try
        {
            json = JsonSerializer.Serialize(body);
        }
        catch (Exception ex)
        {
            return Failure($"Serialize failed: {ex.Message}");
        }

        CadBody? copy;
        try
        {
            copy = JsonSerializer.Deserialize<CadBody>(json);
        }
        catch (Exception ex)
        {
            return Failure($"Deserialize failed: {ex.Message}");
        }

        if (copy is null)
            return Failure("Failed to duplicate body.");

        copy.Id = Guid.NewGuid();
        copy.Name = $"{body.Name} Copy";
        foreach (var feature in copy.Features)
        {
            feature.Id = Guid.NewGuid();
        }

        var existingMove = copy.Features.OfType<MoveFeature>().FirstOrDefault();
        if (existingMove is not null)
        {
            existingMove.X += 20d;
        }
        else
        {
            copy.Features.Add(new MoveFeature { Name = "Move1", X = 20d, Y = 0d, Z = 0d });
        }

        Project.Scene.Bodies.Add(copy);
        Project.Selection = new CadSelection(CadEntityKind.Body, copy.Id, copy.Name);
        return Success($"Duplicated '{body.Name}' as '{copy.Name}'.", true);
    }

    private SketchFeature? FindSketchById(Guid sketchId)
    {
        foreach (var body in Project.Scene.Bodies)
        {
            var found = body.Features.OfType<SketchFeature>().FirstOrDefault(s => s.Id == sketchId);
            if (found is not null)
                return found;
        }

        return null;
    }

    private CadActionResult HandleExtrudeSelectedSketch(CadCommandAction action)
    {
        var selection = ResolveSelectedSketchSelection();
        if (selection.Sketch is null || selection.Body is null)
        {
            return Failure("Select a closed normal sketch profile.");
        }

        var sketch = selection.Sketch;
        var sketchBody = selection.Body;

        if (!sketch.IsClosedProfile)
            return Failure($"{sketch.Name} is open or construction-only. Select a closed normal sketch profile.");

        if (!CanExtrudeSketch(sketch))
            return Failure("Select a closed normal sketch profile. Construction/reference geometry is ignored for extrusion.");

        const double minDepth = 0.2d;
        const double maxDepth = 5000d;
        var clampedDepth = Math.Clamp(Math.Abs(action.Amount) < 0.001d ? 8d : Math.Abs(action.Amount), minDepth, maxDepth);

        // region: task 31 start
        var extrudeOp = action.ExtrudeOp;

        if (extrudeOp is not CadExtrudeOperation.Join and not CadExtrudeOperation.Cut &&
            TryFindExistingSolidCreateFeature(sketchBody, out var existingCreate))
        {
            return Failure(
                $"{sketch.Name} already drives {existingCreate.Name}. Edit that feature or create a new sketch for another body.");
        }

        CadBody? targetBody = null;
        if (extrudeOp == CadExtrudeOperation.Join || extrudeOp == CadExtrudeOperation.Cut)
        {
            var joinCutTargets = ResolveExtrudeJoinCutTargets();
            targetBody = action.TargetBodyId != Guid.Empty
                ? joinCutTargets.FirstOrDefault(b => b.Id == action.TargetBodyId)
                : joinCutTargets.FirstOrDefault();

            if (targetBody is null)
            {
                return Failure(extrudeOp == CadExtrudeOperation.Cut
                    ? "No bodies to cut."
                    : "No bodies to join.");
            }
        }

        var featureName = extrudeOp switch
        {
            CadExtrudeOperation.Join when targetBody is not null => $"Extrude (Join ← {targetBody.Name})",
            CadExtrudeOperation.Cut when targetBody is not null => $"Extrude (Cut ← {targetBody.Name})",
            CadExtrudeOperation.Symmetric => "Extrude (Symmetric)",
            _ => "Extrude"
        };

        var extrude = new ExtrudeFeature
        {
            Name = featureName,
            SketchFeatureId = sketch.Id,
            SketchName = sketch.Name,
            Depth = clampedDepth,
            ReverseDirection = action.Amount < 0d && extrudeOp != CadExtrudeOperation.Symmetric,
            Operation = extrudeOp == CadExtrudeOperation.Symmetric ? CadExtrudeOperation.NewBody : extrudeOp,
            Symmetric = extrudeOp == CadExtrudeOperation.Symmetric,
            TargetBodyId = targetBody?.Id ?? Guid.Empty,
            TargetBodyName = targetBody?.Name ?? string.Empty
        };
        // region: task 31 end

        if (extrudeOp == CadExtrudeOperation.Join || extrudeOp == CadExtrudeOperation.Cut)
        {
            if (targetBody is null)
            {
                return Failure(extrudeOp == CadExtrudeOperation.Cut
                    ? "No bodies to cut."
                    : "No bodies to join.");
            }

            if (targetBody.Id != sketchBody.Id)
            {
                sketchBody.Features.Remove(sketch);
                targetBody.Features.Add(sketch);

                if (sketchBody.Features.Count == 0)
                {
                    Project.Scene.Bodies.Remove(sketchBody);
                }
            }

            targetBody.Features.Add(extrude);
            Project.Selection = new CadSelection(CadEntityKind.Body, targetBody.Id, targetBody.Name);
            Project.ActiveMode = CadMode.DirectPrimitive;

            var joinCutSuffix = extrudeOp == CadExtrudeOperation.Join
                ? $" (join into {targetBody.Name})"
                : $" (cut from {targetBody.Name})";

            return Success(
                $"Applied {featureName} from {sketch.Name} (depth {clampedDepth:0.###} mm){joinCutSuffix}.",
                true);
        }

        sketchBody.Features.Add(extrude);
        sketchBody.Name = $"Part{NextBodyIndex("Part")}";
        Project.Selection = new CadSelection(CadEntityKind.Body, sketchBody.Id, sketchBody.Name);
        Project.ActiveMode = CadMode.DirectPrimitive;

        // region: task 31 start
        var opSuffix = extrudeOp switch
        {
            CadExtrudeOperation.Symmetric => " (symmetric)",
            CadExtrudeOperation.Join when targetBody is not null => $" (joined into {targetBody.Name})",
            CadExtrudeOperation.Cut when targetBody is not null => $" (cut from {targetBody.Name})",
            _ => string.Empty
        };
        // region: task 31 end

        return Success($"Created {sketchBody.Name} from {sketch.Name} (depth {clampedDepth:0.###} mm){opSuffix}.", true);
    }

    private List<CadBody> ResolveExtrudeJoinCutTargets()
    {
        var compiledBodyIds = Compile().Bodies.Select(body => body.BodyId).ToHashSet();
        return Project.Scene.Bodies
            .Where(body => body.Visible && compiledBodyIds.Contains(body.Id))
            .ToList();
    }

    private static bool TryFindExistingSolidCreateFeature(CadBody body, out CadFeature feature)
    {
        feature = body.Features.FirstOrDefault(item =>
            item is PrimitiveFeature or ExtrudeFeature or RevolveFeature or BooleanFeature) ?? null!;
        return feature is not null;
    }

    private CadReferencePlane? ResolveSelectedPlane()
    {
        if (Project.Selection.Kind != CadEntityKind.ReferencePlane)
        {
            return null;
        }

        return Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Id == Project.Selection.EntityId);
    }

    private CadReferencePlane? ResolvePlane(CadCommandAction action)
    {
        if (action.EntityId != Guid.Empty)
        {
            var planeById = Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Id == action.EntityId);
            if (planeById is not null)
            {
                return planeById;
            }
        }

        if (action.PlaneKind is not null)
        {
            var planeByKind = Project.Scene.ReferencePlanes.FirstOrDefault(item => item.Kind == action.PlaneKind.Value);
            if (planeByKind is not null)
            {
                return planeByKind;
            }
        }

        if (string.IsNullOrWhiteSpace(action.EntityName))
        {
            return null;
        }

        return Project.Scene.ReferencePlanes.FirstOrDefault(item =>
            item.Name.Equals(action.EntityName, StringComparison.OrdinalIgnoreCase));
    }

    private CadBody? ResolveSelectedBody()
    {
        if (Project.Selection.Kind == CadEntityKind.Body)
        {
            return Project.Scene.Bodies.FirstOrDefault(item => item.Id == Project.Selection.EntityId);
        }

        if (Project.Selection.Kind is not CadEntityKind.Feature and not CadEntityKind.Sketch)
        {
            return null;
        }

        return Project.Scene.Bodies.FirstOrDefault(body =>
            body.Features.Any(feature => feature.Id == Project.Selection.EntityId));
    }

    private (CadBody? Body, SketchFeature? Sketch) ResolveSelectedSketchSelection()
    {
        if (Project.Selection.Kind == CadEntityKind.Sketch)
        {
            foreach (var body in Project.Scene.Bodies)
            {
                var sketch = body.Features.OfType<SketchFeature>()
                    .FirstOrDefault(feature => feature.Id == Project.Selection.EntityId);
                if (sketch is not null)
                {
                    return (body, sketch);
                }
            }
        }

        if (Project.Selection.Kind == CadEntityKind.SketchEntity)
        {
            foreach (var body in Project.Scene.Bodies)
            {
                foreach (var sketch in body.Features.OfType<SketchFeature>())
                {
                    if (sketch.Entities.Any(entity => entity.Id == Project.Selection.EntityId))
                    {
                        return (body, sketch);
                    }
                }
            }
        }

        if (Project.Selection.Kind == CadEntityKind.Body)
        {
            var body = Project.Scene.Bodies.FirstOrDefault(item => item.Id == Project.Selection.EntityId);
            return (body, body?.Features.OfType<SketchFeature>().LastOrDefault());
        }

        return (null, null);
    }

    private int NextIndex(CadPrimitiveKind primitiveKind)
    {
        return NextBodyIndex(primitiveKind.ToString());
    }

    private int NextBodyIndex(string prefix)
    {
        var max = 0;

        foreach (var body in Project.Scene.Bodies)
        {
            if (!body.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = body.Name[prefix.Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                max = Math.Max(max, value);
            }
        }

        return max + 1;
    }

    private int NextFeatureIndex(CadFeatureKind featureKind)
    {
        var prefix = featureKind.ToString();
        var max = 0;

        foreach (var feature in Project.Scene.Bodies.SelectMany(body => body.Features))
        {
            if (!feature.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = feature.Name[prefix.Length..];
            if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                max = Math.Max(max, value);
            }
        }

        return max + 1;
    }

    private static bool CanExtrudeSketch(SketchFeature sketch) =>
        ProfileBuilder.TryBuild(sketch, out _, out _);

    private CadActionResult HandleRevolveSelectedSketch(CadCommandAction action)
    {
        var selection = ResolveSelectedSketchSelection();
        if (selection.Sketch is null || selection.Body is null)
        {
            return Failure("Select a closed sketch profile.");
        }

        var sketch = selection.Sketch;
        var sketchBody = selection.Body;

        if (!sketch.IsClosedProfile) return Failure($"{sketch.Name} is not a closed profile.");
        if (TryFindExistingSolidCreateFeature(sketchBody, out var existingCreate))
        {
            return Failure(
                $"{sketch.Name} already drives {existingCreate.Name}. Edit that feature or create a new sketch for another body.");
        }

        var angle = action.Amount is > 0 and <= 360 ? action.Amount : 360d;
        var revolve = new RevolveFeature
        {
            Name = $"Revolve{NextFeatureIndex(CadFeatureKind.Revolve)}",
            SketchFeatureId = sketch.Id,
            SketchName = sketch.Name,
            AngleDegrees = angle,
            Axis = action.Axis is CadAxis.X or CadAxis.Y ? action.Axis : CadAxis.Y
        };

        if (!ProfileBuilder.TryBuild(sketch, out var revolveProfile, out var err))
        {
            return Failure(err);
        }

        var revolveAxis = revolve.Axis is CadAxis.X ? ProfileAxis.X : ProfileAxis.Y;
        const double revolveTolerance = 0.0001d;
        var radiusCoordinates = revolveAxis == ProfileAxis.X
            ? revolveProfile.Points.Select(point => point.Y).ToList()
            : revolveProfile.Points.Select(point => point.X).ToList();
        var hasPositive = radiusCoordinates.Any(value => value > revolveTolerance);
        var hasNegative = radiusCoordinates.Any(value => value < -revolveTolerance);
        if (hasPositive && hasNegative)
        {
            return Failure(revolveAxis == ProfileAxis.X
                ? "Profile crosses the selected revolve axis. Keep all profile points on one side of the local X axis."
                : "Profile crosses the selected revolve axis. Keep all profile points on one side of the local Y axis.");
        }

        if (!hasPositive && !hasNegative)
        {
            return Failure("Profile collapses onto the revolve axis.");
        }

        sketchBody.Features.Add(revolve);
        sketchBody.Name = $"Part{NextBodyIndex("Part")}";
        Project.Selection = new CadSelection(CadEntityKind.Body, sketchBody.Id, sketchBody.Name);
        Project.ActiveMode = CadMode.DirectPrimitive;
        return Success($"Created {sketchBody.Name} via revolve ({angle:0.#}° around {revolve.Axis}).", true);
    }

    private CadActionResult HandleSweepSelectedSketch(CadCommandAction action)
    {
        var selection = ResolveSelectedSketchSelection();
        if (selection.Sketch is null || selection.Body is null)
        {
            return Failure("Select a closed sketch profile.");
        }

        var sketch = selection.Sketch;
        var sketchBody = selection.Body;

        if (!sketch.IsClosedProfile) return Failure($"{sketch.Name} is not a closed profile.");
        if (TryFindExistingSolidCreateFeature(sketchBody, out var existingCreate))
        {
            return Failure(
                $"{sketch.Name} already drives {existingCreate.Name}. Edit that feature or create a new sketch.");
        }

        var distance = action.Amount is > 0 ? action.Amount : 20d;
        var twist = action.U;
        var sweep = new SweepFeature
        {
            Name = $"Sweep{NextFeatureIndex(CadFeatureKind.Sweep)}",
            SketchFeatureId = sketch.Id,
            SketchName = sketch.Name,
            Distance = distance,
            TwistDegrees = twist
        };

        if (!ProfileBuilder.TryBuild(sketch, out _, out var err))
        {
            return Failure(err);
        }

        sketchBody.Features.Add(sweep);
        sketchBody.Name = $"Part{NextBodyIndex("Part")}";
        Project.Selection = new CadSelection(CadEntityKind.Body, sketchBody.Id, sketchBody.Name);
        Project.ActiveMode = CadMode.DirectPrimitive;
        return Success($"Created {sketchBody.Name} via sweep ({distance:0.#} along sketch normal).", true);
    }

    private SketchFeature? FindSketchGlobal(Guid sketchFeatureId)
        => Project.Scene.Bodies
            .SelectMany(b => b.Features.OfType<SketchFeature>())
            .FirstOrDefault(s => s.Id == sketchFeatureId);

    private CadActionResult HandleLoftFromProfiles(CadCommandAction action)
    {
        var sketchA = FindSketchGlobal(action.EntityId);
        var sketchB = FindSketchGlobal(action.EntityIdB);

        if (sketchA is null || sketchB is null)
            return Failure("Select two closed sketch profiles for loft.");
        if (!sketchA.IsClosedProfile) return Failure($"{sketchA.Name} is not a closed profile.");
        if (!sketchB.IsClosedProfile) return Failure($"{sketchB.Name} is not a closed profile.");
        if (!ProfileBuilder.TryBuild(sketchA, out _, out var errA)) return Failure(errA);
        if (!ProfileBuilder.TryBuild(sketchB, out _, out var errB)) return Failure(errB);

        var distance = action.Amount > 0 ? action.Amount : 20d;
        var loft = new LoftFeature
        {
            Name = $"Loft{NextFeatureIndex(CadFeatureKind.Loft)}",
            ProfileASketchId = sketchA.Id,
            ProfileASketchName = sketchA.Name,
            ProfileBSketchId = sketchB.Id,
            ProfileBSketchName = sketchB.Name,
            Distance = distance
        };

        var newBody = new CadBody { Name = $"Part{NextBodyIndex("Part")}" };
        newBody.Features.Add(loft);
        Project.Scene.Bodies.Add(newBody);
        Project.Selection = new CadSelection(CadEntityKind.Body, newBody.Id, newBody.Name);
        Project.ActiveMode = CadMode.DirectPrimitive;
        return Success($"Created {newBody.Name} via loft.", true);
    }

    private CadActionResult HandleChamferSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before applying a chamfer.");
        if (!CanApplyExtrudeProfileModifier(body, out var reason))
            return Failure($"Chamfer is limited to simple sketch-extruded bodies for now. {reason}");
        if (body.Features.OfType<ChamferFeature>().Any())
            return Failure($"{body.Name} already has a chamfer.");

        var distance = action.Amount > 0 ? Math.Clamp(action.Amount, 0.1d, 100d) : 1d;
        body.Features.Add(new ChamferFeature
        {
            Name = $"Chamfer{NextFeatureIndex(CadFeatureKind.Chamfer)}",
            Distance = distance
        });
        return Success($"Added chamfer (d={distance:0.###}) to {body.Name}.", true);
    }

    private CadActionResult HandleFilletSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before applying a fillet.");
        if (!CanApplyExtrudeProfileModifier(body, out var reason))
            return Failure($"Fillet is limited to simple sketch-extruded bodies for now. {reason}");
        if (body.Features.OfType<FilletFeature>().Any())
            return Failure($"{body.Name} already has a fillet.");

        var radius = action.Amount > 0 ? Math.Clamp(action.Amount, 0.1d, 100d) : 2d;
        body.Features.Add(new FilletFeature
        {
            Name = $"Fillet{NextFeatureIndex(CadFeatureKind.Fillet)}",
            Radius = radius
        });
        return Success($"Added fillet (r={radius:0.###}) to {body.Name}.", true);
    }

    private CadActionResult HandleShellSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before shelling.");
        if (!CanApplyExtrudeProfileModifier(body, out var reason))
            return Failure($"Shell is limited to simple sketch-extruded bodies for now. {reason}");
        if (body.Features.OfType<ShellFeature>().Any())
            return Failure($"{body.Name} already has a shell feature.");

        var thickness = action.Amount > 0 ? Math.Clamp(action.Amount, 0.1d, 500d) : 2d;
        body.Features.Add(new ShellFeature
        {
            Name = $"Shell{NextFeatureIndex(CadFeatureKind.Shell)}",
            Thickness = thickness
        });
        return Success($"Shelled {body.Name} (wall={thickness:0.###} mm).", true);
    }

    private static bool CanApplyExtrudeProfileModifier(CadBody body, out string reason)
    {
        var hasSolid = false;
        var currentIsSimpleExtrude = false;

        foreach (var feature in body.Features.Where(feature => !feature.Suppressed))
        {
            switch (feature)
            {
                case SketchFeature:
                    break;

                case PrimitiveFeature primitive when !hasSolid:
                    hasSolid = true;
                    currentIsSimpleExtrude = false;
                    reason = $"{body.Name} is based on {primitive.Name}, not a sketch extrude.";
                    return false;

                case ExtrudeFeature extrude when !hasSolid:
                    hasSolid = true;
                    currentIsSimpleExtrude = extrude.Operation is CadExtrudeOperation.NewBody or CadExtrudeOperation.Symmetric;
                    break;

                case RevolveFeature revolve when !hasSolid:
                    hasSolid = true;
                    currentIsSimpleExtrude = false;
                    reason = $"{body.Name} is based on {revolve.Name}, not a sketch extrude.";
                    return false;

                case FilletFeature or ChamferFeature or ShellFeature when currentIsSimpleExtrude:
                    break;

                case MoveFeature or MirrorFeature or LinearPatternFeature or CircularPatternFeature or BooleanFeature or HoleFeature:
                    if (hasSolid)
                    {
                        currentIsSimpleExtrude = false;
                    }
                    break;

                case ExtrudeFeature extrude when hasSolid &&
                    extrude.Operation is CadExtrudeOperation.Join or CadExtrudeOperation.Cut:
                    currentIsSimpleExtrude = false;
                    break;
            }
        }

        if (!hasSolid)
        {
            reason = "Create or select an extruded body first.";
            return false;
        }

        if (!currentIsSimpleExtrude)
        {
            reason = "Add this modifier before move, mirror, pattern, boolean, hole, join, or cut operations.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private CadActionResult HandleMirrorSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before mirroring.");

        var axis = action.Axis switch
        {
            CadAxis.Y => MirrorAxis.Y,
            CadAxis.Z => MirrorAxis.Z,
            _ => MirrorAxis.X
        };

        body.Features.Add(new MirrorFeature
        {
            Name = $"Mirror{NextFeatureIndex(CadFeatureKind.Mirror)}",
            Axis = axis
        });
        return Success($"Mirrored {body.Name} across {axis} plane.", true);
    }

    private CadActionResult HandleLinearPatternSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before applying a linear pattern.");

        var count = Math.Max(2, (int)Math.Round(action.Amount));
        var spacing = action.U > 0 ? action.U : 20d;
        body.Features.Add(new LinearPatternFeature
        {
            Name = $"LinearPattern{NextFeatureIndex(CadFeatureKind.LinearPattern)}",
            Count = count,
            Spacing = spacing,
            Axis = action.Axis
        });
        return Success($"Linear pattern: {count} copies along {action.Axis}, spacing {spacing:0.###}.", true);
    }

    private CadActionResult HandleCircularPatternSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before applying a circular pattern.");

        var count = Math.Max(2, (int)Math.Round(action.Amount));
        var totalAngle = action.U > 0 ? action.U : 360d;
        body.Features.Add(new CircularPatternFeature
        {
            Name = $"Circular Pattern ({count} × {body.Name})",
            Count = count,
            TotalAngle = totalAngle,
            Axis = action.Axis
        });
        return Success($"Circular pattern: {count} copies, {totalAngle:0.###}° around {action.Axis}.", true);
    }

    private CadActionResult HandleHoleSelectedBody(CadCommandAction action)
    {
        var body = ResolveSelectedBody();
        if (body is null) return Failure("Select a body before adding a hole.");

        var diameter = action.Amount > 0 ? action.Amount : 10d;
        var centerOffsetX = action.U;
        var centerOffsetY = action.V;

        HoleDepthKind depthKind;
        double depthValue;
        var depthInfo = action.EntityName ?? "ThroughAll";
        if (depthInfo.StartsWith("Blind:", StringComparison.OrdinalIgnoreCase))
        {
            depthKind = HoleDepthKind.Blind;
            depthValue = double.TryParse(depthInfo["Blind:".Length..], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? Math.Max(0.01d, parsed) : 20d;
        }
        else
        {
            depthKind = HoleDepthKind.ThroughAll;
            depthValue = 10000d;
        }

        var depthLabel = depthKind == HoleDepthKind.ThroughAll ? "Through" : $"{depthValue:0.###}mm";
        body.Features.Add(new HoleFeature
        {
            Name = $"Hole (⌀{diameter:0.###} × {depthLabel})",
            Diameter = diameter,
            DepthKind = depthKind,
            DepthValue = depthValue,
            CenterOffsetX = centerOffsetX,
            CenterOffsetY = centerOffsetY
        });

        return Success($"Added {body.Features.Last().Name} to {body.Name}.", true);
    }

    private CadActionResult HandleBooleanOperation(CadCommandAction action, BooleanOperation operation)
    {
        var bodyAId = action.EntityId;
        var bodyBId = action.EntityIdB;

        if (bodyAId == Guid.Empty || bodyBId == Guid.Empty)
            return Failure("Boolean operation requires two body IDs.");

        var bodyA = Project.Scene.Bodies.FirstOrDefault(b => b.Id == bodyAId);
        var bodyB = Project.Scene.Bodies.FirstOrDefault(b => b.Id == bodyBId);

        if (bodyA is null) return Failure("Body A not found.");
        if (bodyB is null) return Failure("Body B not found.");

        bodyA.Visible = false;
        bodyB.Visible = false;

        var opName = operation.ToString();
        var resultName = $"{opName}_{bodyA.Name}_{bodyB.Name}";
        var boolBody = new CadBody { Name = resultName };
        boolBody.Features.Add(new BooleanFeature
        {
            Name = opName,
            Operation = operation,
            BodyAId = bodyAId,
            BodyBId = bodyBId,
            BodyAName = bodyA.Name,
            BodyBName = bodyB.Name
        });

        Project.Scene.Bodies.Add(boolBody);
        Project.Selection = new CadSelection(CadEntityKind.Body, boolBody.Id, boolBody.Name);
        Project.ActiveMode = CadMode.Boolean;

        return Success($"{opName}: {bodyA.Name} + {bodyB.Name} → {resultName}.", true);
    }

    private static List<CadSketchEntity>? CreatePlacedRectangleLines(
        CadSketchSession session,
        Vector2D point,
        out string? message)
    {
        if (session.PendingShapeAnchor is null)
        {
            session.PendingShapeAnchor = point;
            message = "Rectangle first corner placed. Click the opposite corner.";
            return null;
        }

        var anchor = session.PendingShapeAnchor.Value;
        session.PendingShapeAnchor = null;

        if (Distance(anchor, point) <= 0.001d)
        {
            message = "Rectangle ignored because both corners are the same point.";
            return null;
        }

        var minX = Math.Min(anchor.X, point.X);
        var minY = Math.Min(anchor.Y, point.Y);
        var maxX = Math.Max(anchor.X, point.X);
        var maxY = Math.Max(anchor.Y, point.Y);

        message = "Added rectangle as connected lines.";

        return CreateRectangleLines(minX, minY, maxX, maxY)
            .Cast<CadSketchEntity>()
            .ToList();
    }

    private static CadSketchCircle? CreatePlacedCircle(CadSketchSession session, Vector2D point, out string? message)
    {
        if (session.PendingShapeAnchor is null)
        {
            session.PendingShapeAnchor = point;
            message = "Circle center placed. Click to define the radius.";
            return null;
        }

        var center = session.PendingShapeAnchor.Value;
        session.PendingShapeAnchor = null;
        var radius = Distance(center, point);
        if (radius <= 0.001d)
        {
            message = "Circle ignored because the radius is too small.";
            return null;
        }

        message = "Added circle.";
        return new CadSketchCircle
        {
            CenterX = center.X,
            CenterY = center.Y,
            Radius = radius
        };
    }

    private static CadSketchArc? CreatePlacedArc(CadSketchSession session, Vector2D point, out string? message)
    {
        session.PendingSketchPoints.Add(point);
        switch (session.PendingSketchPoints.Count)
        {
            case 1:
                message = "Arc start point placed. Click the arc end point.";
                return null;
            case 2:
                message = "Arc end point placed. Click a point on the arc.";
                return null;
        }

        var start = session.PendingSketchPoints[0];
        var end = session.PendingSketchPoints[1];
        var through = session.PendingSketchPoints[2];
        session.PendingSketchPoints.Clear();

        if (!TryCreateThreePointArc(start, end, through, out var arc))
        {
            message = "Arc ignored because the three points are collinear or invalid.";
            return null;
        }

        message = "Added three-point arc.";
        return arc;
    }

    private static CadSketchPoint CreatePlacedPoint(Vector2D point)
    {
        return new CadSketchPoint
        {
            X = point.X,
            Y = point.Y
        };
    }

    private static CadSketchPolygon? CreatePlacedPolygon(CadSketchSession session, Vector2D point, out string? message)
    {
        if (session.PendingShapeAnchor is null)
        {
            session.PendingShapeAnchor = point;
            message = "Polygon center placed. Click to define the radius.";
            return null;
        }

        var center = session.PendingShapeAnchor.Value;
        session.PendingShapeAnchor = null;
        var radius = Distance(center, point);
        if (radius <= 0.001d)
        {
            message = "Polygon ignored because radius is too small.";
            return null;
        }

        message = $"Added {session.PendingPolygonSides}-sided polygon.";
        return new CadSketchPolygon
        {
            CenterX = center.X,
            CenterY = center.Y,
            Radius = radius,
            Sides = session.PendingPolygonSides
        };
    }

    private static CadSketchSlot? CreatePlacedSlot(CadSketchSession session, Vector2D point, out string? message)
    {
        session.PendingSketchPoints.Add(point);
        switch (session.PendingSketchPoints.Count)
        {
            case 1:
                message = "Slot: first center placed. Click the second center.";
                return null;
            case 2:
                message = "Slot: second center placed. Click to set the radius.";
                return null;
        }

        var c1 = session.PendingSketchPoints[0];
        var c2 = session.PendingSketchPoints[1];
        var radiusPoint = session.PendingSketchPoints[2];
        session.PendingSketchPoints.Clear();

        var dx = c2.X - c1.X;
        var dy = c2.Y - c1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double perp;
        if (len < 0.001d)
        {
            perp = Distance(c1, radiusPoint);
        }
        else
        {
            var nx = -dy / len;
            var ny = dx / len;
            var relX = radiusPoint.X - c1.X;
            var relY = radiusPoint.Y - c1.Y;
            perp = Math.Abs((relX * nx) + (relY * ny));
        }

        var radius = Math.Max(perp, 0.2d);
        message = "Added slot.";
        return new CadSketchSlot
        {
            Center1X = c1.X,
            Center1Y = c1.Y,
            Center2X = c2.X,
            Center2Y = c2.Y,
            Radius = radius
        };
    }

    private static IReadOnlyList<CadSketchEntity>? CreateOrExtendSpline(CadSketchSession session, Vector2D point, out string? message)
    {
        session.PendingSketchPoints.Add(point);
        if (session.PendingSketchPoints.Count < 2)
        {
            message = "Spline: control point added. Click more points, then Esc or finish sketch to commit.";
            return null;
        }

        message = $"Spline control point #{session.PendingSketchPoints.Count} added.";
        return null;
    }

    private static IReadOnlyList<CadSketchEntity>? CreatePlacedOffset(CadSketchSession session, Vector2D point, out string? message)
    {
        if (session.DraftEntities.Count == 0)
        {
            message = "Offset: draw geometry first, then click near a segment to offset it.";
            return null;
        }

        var distance = session.PendingOffsetDistance;
        var results = new List<CadSketchEntity>();
        foreach (var entity in session.DraftEntities)
        {
            switch (entity)
            {
                case CadSketchLine line:
                {
                    var dx = line.EndX - line.StartX;
                    var dy = line.EndY - line.StartY;
                    var len = Math.Sqrt((dx * dx) + (dy * dy));
                    if (len < 0.001d)
                    {
                        break;
                    }

                    var nx = -dy / len * distance;
                    var ny = dx / len * distance;
                    results.Add(new CadSketchLine
                    {
                        StartX = line.StartX + nx,
                        StartY = line.StartY + ny,
                        EndX = line.EndX + nx,
                        EndY = line.EndY + ny
                    });
                    break;
                }

                case CadSketchCircle circle:
                    results.Add(new CadSketchCircle
                    {
                        CenterX = circle.CenterX,
                        CenterY = circle.CenterY,
                        Radius = Math.Max(circle.Radius + distance, 0.2d)
                    });
                    break;
            }
        }

        if (results.Count == 0)
        {
            message = "Offset: no offsettable entities found (lines or circles only).";
            return null;
        }

        message = $"Added offset ({distance:0.###} mm).";
        return results;
    }

    private static IReadOnlyList<CadSketchEntity>? CreatePlacedFillet2d(CadSketchSession session, Vector2D point, out string? message)
    {
        var lines = session.DraftEntities.OfType<CadSketchLine>().ToList();
        if (lines.Count < 2)
        {
            message = "Fillet2D: need at least 2 line segments in the sketch.";
            return null;
        }

        var last = lines[^1];
        var prev = lines[^2];
        var radius = session.PendingFilletRadius;

        if (!TryComputeFillet2d(prev, last, radius, out var arc))
        {
            message = "Fillet2D: lines are parallel or too short for this radius.";
            return null;
        }

        message = $"Added 2D fillet (r={radius:0.###}).";
        return [arc];
    }

    private static bool TryComputeFillet2d(CadSketchLine a, CadSketchLine b, double radius, out CadSketchArc arc)
    {
        arc = new CadSketchArc();
        var d1X = a.EndX - a.StartX;
        var d1Y = a.EndY - a.StartY;
        var len1 = Math.Sqrt((d1X * d1X) + (d1Y * d1Y));
        var d2X = b.EndX - b.StartX;
        var d2Y = b.EndY - b.StartY;
        var len2 = Math.Sqrt((d2X * d2X) + (d2Y * d2Y));
        if (len1 < 0.001d || len2 < 0.001d)
        {
            return false;
        }

        var u1X = d1X / len1;
        var u1Y = d1Y / len1;
        var u2X = d2X / len2;
        var u2Y = d2Y / len2;
        var cross = (u1X * u2Y) - (u1Y * u2X);
        if (Math.Abs(cross) < 0.001d)
        {
            return false;
        }

        var n1X = -u1Y;
        var n1Y = u1X;
        var n2X = -u2Y;
        var n2Y = u2X;
        var sign = cross > 0 ? 1d : -1d;

        var cx1 = a.EndX + (sign * radius * n1X);
        var cy1 = a.EndY + (sign * radius * n1Y);
        var cx2 = b.StartX + (sign * radius * n2X);
        var cy2 = b.StartY + (sign * radius * n2Y);

        var centerX = (cx1 + cx2) / 2d;
        var centerY = (cy1 + cy2) / 2d;

        var startAngle = Math.Atan2(a.EndY - centerY, a.EndX - centerX) * 180d / Math.PI;
        var endAngle = Math.Atan2(b.StartY - centerY, b.StartX - centerX) * 180d / Math.PI;

        arc = new CadSketchArc
        {
            CenterX = centerX,
            CenterY = centerY,
            Radius = radius,
            StartAngleDegrees = startAngle,
            EndAngleDegrees = endAngle,
            CounterClockwise = cross < 0
        };
        return true;
    }

    private static IReadOnlyList<CadSketchEntity>? CreatePlacedMirror(CadSketchSession session, Vector2D point, out string? message)
    {
        if (session.DraftEntities.Count == 0)
        {
            message = "Mirror: draw geometry first, then click to define the mirror axis.";
            return null;
        }

        // First click: record axis start
        if (session.PendingShapeAnchor is null)
        {
            session.PendingShapeAnchor = point;
            message = "Mirror axis start placed. Click a second point to define the axis direction.";
            return null;
        }

        // Second click: perform mirror across axis through anchor→point
        var axisStart = session.PendingShapeAnchor.Value;
        session.PendingShapeAnchor = null;

        if (Distance(axisStart, point) <= 0.001d)
        {
            message = "Mirror axis is too short — try again.";
            return null;
        }

        var dx = point.X - axisStart.X;
        var dy = point.Y - axisStart.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var dNormX = dx / len;
        var dNormY = dy / len;
        var axisAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        var mirrored = ReflectEntities(session.DraftEntities, axisStart.X, axisStart.Y, dNormX, dNormY, axisAngle);
        var skipped = session.DraftEntities.Count(e => e is not CadSketchLine and not CadSketchCircle and not CadSketchArc
            and not CadSketchSlot and not CadSketchPolygon and not CadSketchSpline);

        if (mirrored.Count == 0)
        {
            message = skipped > 0
                ? "Mirror: no supported entities found."
                : "Mirror: no entities to mirror.";
            return null;
        }

        message = skipped > 0
            ? $"Mirror applied — {mirrored.Count} entit{(mirrored.Count == 1 ? "y" : "ies")} mirrored; {skipped} unsupported type(s) skipped."
            : $"Mirror applied — {mirrored.Count} entit{(mirrored.Count == 1 ? "y" : "ies")} mirrored.";
        return mirrored;
    }

    private static IReadOnlyList<CadSketchEntity>? CreatePlacedTrim(CadSketchSession session, Vector2D point, out string? message)
    {
        const double HitThreshold = 5.0;

        var hit = FindTrimHit(session.DraftEntities, point, HitThreshold);
        if (hit.Entity is null)
        {
            message = "No entity found near click — try clicking closer to a line or arc.";
            return null;
        }

        var others = session.DraftEntities.Where(e => e.Id != hit.Entity.Id).ToList();

        List<CadSketchEntity> replacements;
        switch (hit.Entity)
        {
            case CadSketchLine clickedLine:
            {
                var tParams = CollectLineIntersectionParams(clickedLine, others);
                if (tParams.Count == 0)
                {
                    message = "No intersection found near click — trim requires the entity to cross another.";
                    return null;
                }
                replacements = TrimLine(clickedLine, hit.T, tParams);
                break;
            }
            case CadSketchArc clickedArc:
            {
                var tParams = CollectArcIntersectionAngles(clickedArc, others);
                if (tParams.Count == 0)
                {
                    message = "No intersection found near click — trim requires the entity to cross another.";
                    return null;
                }
                replacements = TrimArc(clickedArc, hit.T, tParams);
                break;
            }
            case CadSketchCircle clickedCircle:
            {
                var angles = CollectCircleIntersectionAngles(clickedCircle, others);
                if (angles.Count < 2)
                {
                    message = angles.Count == 0
                        ? "Circle has no intersections — add a crossing entity first."
                        : "Circle needs at least 2 intersections to trim — add a crossing entity first.";
                    return null;
                }
                replacements = TrimCircle(clickedCircle, hit.T, angles);
                break;
            }
            default:
                message = "Trim is not supported for this entity type.";
                return null;
        }

        session.DraftEntities.Remove(hit.Entity);
        message = replacements.Count > 0
            ? $"Trim applied — {replacements.Count} segment(s) kept."
            : "Trim applied — segment fully removed.";
        return replacements;
    }

    private static IReadOnlyList<CadSketchEntity>? HandleAngleDimensionStep(
        CadSketchSession session, Vector2D point, out string? message)
    {
        const double HitThreshold = 5.0;

        var hit = FindNearestLine(session.DraftEntities, point, HitThreshold);
        if (hit is null)
        {
            message = "Click on a line to start the angle dimension.";
            return null;
        }

        if (session.PendingAngleDimFirstId is null)
        {
            session.PendingAngleDimFirstId = hit.Id;
            message = "Line A selected. Click a second line to complete the angle dimension.";
            return null;
        }

        var lineA = session.DraftEntities.OfType<CadSketchLine>()
            .FirstOrDefault(l => l.Id == session.PendingAngleDimFirstId.Value);
        var lineB = hit;
        session.PendingAngleDimFirstId = null;

        if (lineA is null || lineA.Id == lineB.Id)
        {
            message = "Please click two different lines.";
            return null;
        }

        var dx1 = lineA.EndX - lineA.StartX;
        var dy1 = lineA.EndY - lineA.StartY;
        var dx2 = lineB.EndX - lineB.StartX;
        var dy2 = lineB.EndY - lineB.StartY;

        var cross = Math.Abs(dx1 * dy2 - dy1 * dx2);
        var dot = Math.Abs(dx1 * dx2 + dy1 * dy2);
        var angleDeg = Math.Atan2(cross, dot) * 180.0 / Math.PI;

        if (angleDeg < 0.1)
        {
            message = "Lines are parallel — angle is 0° or 180°; use linear dimension.";
            return null;
        }

        var dofBefore = ComputeSketchDof(session);
        var isDriven = dofBefore <= 0;
        var label = isDriven ? $"({angleDeg:0.#}°)" : $"{angleDeg:0.#}°";
        var dim = new CadSketchDimension(
            CadSketchDimensionKind.Angle,
            label,
            angleDeg,
            [lineA.Id, lineB.Id],
            "Angle",
            IsDriven: isDriven);

        session.ManualDimensions.Add(dim);
        SolveSketchSession(session);
        message = isDriven
            ? $"Sketch is fully constrained — angle dimension added as reference: {label}"
            : $"Angle dimension placed: {label}";
        return [];
    }

    private static IReadOnlyList<CadSketchEntity>? HandleLinearDimensionStep(
        CadSketchSession session, Vector2D point, out string? message)
    {
        const double HitThreshold = 5.0;

        // Pick a line entity to dimension
        var hit = FindNearestLine(session.DraftEntities, point, HitThreshold);
        if (hit is null)
        {
            message = "Click a line to place a linear dimension.";
            return null;
        }

        var dx = hit.EndX - hit.StartX;
        var dy = hit.EndY - hit.StartY;
        var length = Math.Round(Math.Sqrt(dx * dx + dy * dy), 6);

        // Check if dimension already exists for this entity
        if (session.ManualDimensions.Any(d =>
                d.Kind == CadSketchDimensionKind.Length && d.EntityIds.Contains(hit.Id)))
        {
            message = "Linear dimension already exists for this line.";
            return [];
        }

        var dofBefore = ComputeSketchDof(session);
        var isDriven = dofBefore <= 0;
        var label = isDriven ? $"({length:0.###})" : $"{length:0.###}";
        var dim = new CadSketchDimension(
            CadSketchDimensionKind.Length,
            label,
            length,
            [hit.Id],
            "Length",
            IsDriven: isDriven);

        session.ManualDimensions.Add(dim);
        SolveSketchSession(session);
        message = isDriven
            ? $"Sketch is fully constrained — linear dimension added as reference: {label}"
            : $"Linear dimension placed: {label} mm";
        return [];
    }

    private static IReadOnlyList<CadSketchEntity>? HandleRadiusDimensionStep(
        CadSketchSession session, Vector2D point, out string? message)
    {
        const double HitThreshold = 5.0;

        // Try to hit a circle or arc
        CadSketchEntity? hit = null;
        double bestDist = HitThreshold;

        foreach (var entity in session.DraftEntities)
        {
            if (entity is CadSketchCircle circle)
            {
                var dx = point.X - circle.CenterX;
                var dy = point.Y - circle.CenterY;
                var dist = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - circle.Radius);
                if (dist < bestDist) { bestDist = dist; hit = entity; }
            }
            else if (entity is CadSketchArc arc)
            {
                var dx = point.X - arc.CenterX;
                var dy = point.Y - arc.CenterY;
                var dist = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - arc.Radius);
                if (dist < bestDist) { bestDist = dist; hit = entity; }
            }
        }

        if (hit is null)
        {
            message = "Click a circle or arc to place a radius dimension.";
            return null;
        }

        var (radius, kind, paramKey) = hit switch
        {
            CadSketchCircle c => (c.Radius, CadSketchDimensionKind.Radius, "Radius"),
            CadSketchArc a => (a.Radius, CadSketchDimensionKind.Radius, "Radius"),
            _ => (0d, CadSketchDimensionKind.Radius, "Radius")
        };

        if (session.ManualDimensions.Any(d => d.EntityIds.Contains(hit.Id)
                && d.Kind is CadSketchDimensionKind.Radius or CadSketchDimensionKind.Diameter))
        {
            message = "Radius/diameter dimension already exists for this entity.";
            return [];
        }

        radius = Math.Round(radius, 6);
        var dofBefore = ComputeSketchDof(session);
        var isDriven = dofBefore <= 0;
        var label = isDriven ? $"(R{radius:0.###})" : $"R{radius:0.###}";
        var dim = new CadSketchDimension(
            kind,
            label,
            radius,
            [hit.Id],
            paramKey,
            IsDriven: isDriven);

        session.ManualDimensions.Add(dim);
        SolveSketchSession(session);
        message = isDriven
            ? $"Sketch is fully constrained — radius dimension added as reference: {label}"
            : $"Radius dimension placed: {label} mm";
        return [];
    }

    private static CadSketchLine? FindNearestLine(
        IReadOnlyList<CadSketchEntity> entities, Vector2D point, double threshold)
    {
        CadSketchLine? best = null;
        double bestDist = threshold;

        foreach (var entity in entities.OfType<CadSketchLine>())
        {
            var (dist, _) = PointToEntityDist(entity, point);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = entity;
            }
        }

        return best;
    }

    private static (CadSketchEntity? Entity, double T) FindTrimHit(
        IReadOnlyList<CadSketchEntity> entities, Vector2D point, double threshold)
    {
        CadSketchEntity? best = null;
        double bestDist = threshold;
        double bestT = 0d;

        foreach (var entity in entities)
        {
            var (dist, t) = PointToEntityDist(entity, point);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestT = t;
                best = entity;
            }
        }
        return (best, bestT);
    }

    private static (double Dist, double T) PointToEntityDist(CadSketchEntity entity, Vector2D point)
    {
        switch (entity)
        {
            case CadSketchLine line:
            {
                var dx = line.EndX - line.StartX;
                var dy = line.EndY - line.StartY;
                var lenSq = (dx * dx) + (dy * dy);
                if (lenSq < 1e-12d)
                    return (Distance(point, new Vector2D(line.StartX, line.StartY)), 0d);
                var t = Math.Clamp(((point.X - line.StartX) * dx + (point.Y - line.StartY) * dy) / lenSq, 0d, 1d);
                var px = line.StartX + (t * dx);
                var py = line.StartY + (t * dy);
                return (Math.Sqrt(((point.X - px) * (point.X - px)) + ((point.Y - py) * (point.Y - py))), t);
            }
            case CadSketchCircle circle:
            {
                var angle = NormalizeAngle(Math.Atan2(point.Y - circle.CenterY, point.X - circle.CenterX) * 180d / Math.PI);
                var distC = Math.Sqrt(((point.X - circle.CenterX) * (point.X - circle.CenterX)) + ((point.Y - circle.CenterY) * (point.Y - circle.CenterY)));
                return (Math.Abs(distC - circle.Radius), angle);
            }
            case CadSketchArc arc:
            {
                var angle = NormalizeAngle(Math.Atan2(point.Y - arc.CenterY, point.X - arc.CenterX) * 180d / Math.PI);
                if (!AngleLiesOnArc(arc, angle))
                {
                    var startPt = ArcEndpoint(arc, arc.StartAngleDegrees);
                    var endPt = ArcEndpoint(arc, arc.EndAngleDegrees);
                    var dStart = Distance(point, startPt);
                    var dEnd = Distance(point, endPt);
                    return dStart <= dEnd ? (dStart, arc.StartAngleDegrees) : (dEnd, arc.EndAngleDegrees);
                }
                var distC2 = Math.Sqrt(((point.X - arc.CenterX) * (point.X - arc.CenterX)) + ((point.Y - arc.CenterY) * (point.Y - arc.CenterY)));
                return (Math.Abs(distC2 - arc.Radius), angle);
            }
            default:
                return (double.MaxValue, 0d);
        }
    }

    private static Vector2D ArcEndpoint(CadSketchArc arc, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180d;
        return new Vector2D(arc.CenterX + (arc.Radius * Math.Cos(rad)), arc.CenterY + (arc.Radius * Math.Sin(rad)));
    }

    private static bool AngleLiesOnArc(CadSketchArc arc, double angle) =>
        arc.CounterClockwise
            ? AngleLiesOnSweep(arc.StartAngleDegrees, arc.EndAngleDegrees, angle)
            : AngleLiesOnSweep(arc.EndAngleDegrees, arc.StartAngleDegrees, angle);

    private static List<double> CollectLineIntersectionParams(CadSketchLine line, IEnumerable<CadSketchEntity> others)
    {
        var tValues = new List<double>();

        foreach (var other in others)
        {
            switch (other)
            {
                case CadSketchLine otherLine:
                    if (LineLineIntersectT(line, otherLine) is { } t)
                        tValues.Add(t);
                    break;
                case CadSketchCircle circle:
                    tValues.AddRange(LineCircleIntersectT(line, circle.CenterX, circle.CenterY, circle.Radius));
                    break;
                case CadSketchArc arc:
                    tValues.AddRange(LineArcIntersectT(line, arc));
                    break;
            }
        }

        tValues.Sort();
        return DeduplicateAscending(tValues);
    }

    private static List<double> CollectArcIntersectionAngles(CadSketchArc arc, IEnumerable<CadSketchEntity> others)
    {
        const double Eps = 1e-6d;
        var angles = new List<double>();

        foreach (var other in others)
        {
            switch (other)
            {
                case CadSketchLine line:
                    foreach (var t in LineCircleIntersectT(line, arc.CenterX, arc.CenterY, arc.Radius))
                    {
                        var px = line.StartX + (t * (line.EndX - line.StartX));
                        var py = line.StartY + (t * (line.EndY - line.StartY));
                        var a = PointToAngleDegrees(px, py, arc.CenterX, arc.CenterY);
                        if (AngleLiesOnArc(arc, a))
                            angles.Add(a);
                    }
                    break;
                case CadSketchCircle circle:
                    foreach (var (px, py) in CircleCircleIntersections(arc.CenterX, arc.CenterY, arc.Radius, circle.CenterX, circle.CenterY, circle.Radius))
                    {
                        var a = PointToAngleDegrees(px, py, arc.CenterX, arc.CenterY);
                        if (AngleLiesOnArc(arc, a))
                            angles.Add(a);
                    }
                    break;
                case CadSketchArc otherArc:
                    foreach (var (px, py) in CircleCircleIntersections(arc.CenterX, arc.CenterY, arc.Radius, otherArc.CenterX, otherArc.CenterY, otherArc.Radius))
                    {
                        var aOnThis = PointToAngleDegrees(px, py, arc.CenterX, arc.CenterY);
                        var aOnOther = PointToAngleDegrees(px, py, otherArc.CenterX, otherArc.CenterY);
                        if (AngleLiesOnArc(arc, aOnThis) && AngleLiesOnArc(otherArc, aOnOther))
                            angles.Add(aOnThis);
                    }
                    break;
            }
        }

        var startA = NormalizeAngle(arc.StartAngleDegrees);
        var sweep = ArcSweep(arc);
        angles.Sort((a, b) => ArcParam(arc, a, startA, sweep).CompareTo(ArcParam(arc, b, startA, sweep)));

        // Deduplicate by arc parameter distance
        var deduped = new List<double>();
        foreach (var a in angles)
        {
            var p = ArcParam(arc, a, startA, sweep);
            if (deduped.Count == 0 || Math.Abs(ArcParam(arc, deduped[^1], startA, sweep) - p) > Eps)
                deduped.Add(a);
        }
        return deduped;
    }

    private static double ArcSweep(CadSketchArc arc)
    {
        var start = NormalizeAngle(arc.StartAngleDegrees);
        var end = NormalizeAngle(arc.EndAngleDegrees);
        var sweep = arc.CounterClockwise ? end - start : start - end;
        if (sweep <= 0d) sweep += 360d;
        return sweep;
    }

    private static double ArcParam(CadSketchArc arc, double angle, double startA, double sweep)
    {
        var t = arc.CounterClockwise ? NormalizeAngle(angle) - startA : startA - NormalizeAngle(angle);
        if (t < 0d) t += 360d;
        return t;
    }

    private static List<CadSketchEntity> TrimLine(CadSketchLine line, double tClick, List<double> tParams)
    {
        const double Eps = 1e-6d;
        var result = new List<CadSketchEntity>();

        var allT = new List<double> { 0d };
        foreach (var t in tParams)
            if (t > Eps && t < 1d - Eps)
                allT.Add(t);
        allT.Add(1d);

        // Deduplicate again after adding sentinels
        for (var i = allT.Count - 1; i > 0; i--)
            if (allT[i] - allT[i - 1] < Eps)
                allT.RemoveAt(i);

        var clickInterval = 0;
        for (var i = 0; i < allT.Count - 1; i++)
            if (tClick >= allT[i] - Eps && tClick <= allT[i + 1] + Eps)
            { clickInterval = i; break; }

        for (var i = 0; i < allT.Count - 1; i++)
        {
            if (i == clickInterval) continue;
            var t0 = allT[i]; var t1 = allT[i + 1];
            if (t1 - t0 < Eps) continue;
            result.Add(new CadSketchLine
            {
                StartX = line.StartX + (t0 * (line.EndX - line.StartX)),
                StartY = line.StartY + (t0 * (line.EndY - line.StartY)),
                EndX = line.StartX + (t1 * (line.EndX - line.StartX)),
                EndY = line.StartY + (t1 * (line.EndY - line.StartY)),
            });
        }
        return result;
    }

    private static List<CadSketchEntity> TrimArc(CadSketchArc arc, double clickAngle, List<double> intersectionAngles)
    {
        const double Eps = 1e-6d;
        var result = new List<CadSketchEntity>();

        var startA = NormalizeAngle(arc.StartAngleDegrees);
        var sweep = ArcSweep(arc);

        var tParams = intersectionAngles
            .Select(a => ArcParam(arc, a, startA, sweep))
            .Where(t => t > Eps && t < sweep - Eps)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        if (tParams.Count == 0)
            return result;

        var tClick = ArcParam(arc, clickAngle, startA, sweep);

        var allT = new List<double> { 0d };
        allT.AddRange(tParams);
        allT.Add(sweep);

        var clickInterval = 0;
        for (var i = 0; i < allT.Count - 1; i++)
            if (tClick >= allT[i] - Eps && tClick <= allT[i + 1] + Eps)
            { clickInterval = i; break; }

        for (var i = 0; i < allT.Count - 1; i++)
        {
            if (i == clickInterval) continue;
            var t0 = allT[i]; var t1 = allT[i + 1];
            if (t1 - t0 < Eps) continue;

            double a0, a1;
            if (arc.CounterClockwise)
            {
                a0 = NormalizeAngle(startA + t0);
                a1 = NormalizeAngle(startA + t1);
            }
            else
            {
                a0 = NormalizeAngle(startA - t0);
                a1 = NormalizeAngle(startA - t1);
            }

            result.Add(new CadSketchArc
            {
                CenterX = arc.CenterX,
                CenterY = arc.CenterY,
                Radius = arc.Radius,
                StartAngleDegrees = a0,
                EndAngleDegrees = a1,
                CounterClockwise = arc.CounterClockwise,
            });
        }
        return result;
    }

    private static List<double> CollectCircleIntersectionAngles(CadSketchCircle circle, IEnumerable<CadSketchEntity> others)
    {
        var angles = new List<double>();

        foreach (var other in others)
        {
            switch (other)
            {
                case CadSketchLine line:
                    foreach (var t in LineCircleIntersectT(line, circle.CenterX, circle.CenterY, circle.Radius))
                    {
                        var px = line.StartX + (t * (line.EndX - line.StartX));
                        var py = line.StartY + (t * (line.EndY - line.StartY));
                        angles.Add(PointToAngleDegrees(px, py, circle.CenterX, circle.CenterY));
                    }
                    break;
                case CadSketchArc otherArc:
                    foreach (var (px, py) in CircleCircleIntersections(circle.CenterX, circle.CenterY, circle.Radius, otherArc.CenterX, otherArc.CenterY, otherArc.Radius))
                    {
                        var aOnOther = PointToAngleDegrees(px, py, otherArc.CenterX, otherArc.CenterY);
                        if (AngleLiesOnArc(otherArc, aOnOther))
                            angles.Add(PointToAngleDegrees(px, py, circle.CenterX, circle.CenterY));
                    }
                    break;
                case CadSketchCircle otherCircle:
                    foreach (var (px, py) in CircleCircleIntersections(circle.CenterX, circle.CenterY, circle.Radius, otherCircle.CenterX, otherCircle.CenterY, otherCircle.Radius))
                        angles.Add(PointToAngleDegrees(px, py, circle.CenterX, circle.CenterY));
                    break;
            }
        }

        angles.Sort();
        return DeduplicateAscending(angles, 1e-6d);
    }

    private static List<CadSketchEntity> TrimCircle(CadSketchCircle circle, double clickAngle, List<double> angles)
    {
        const double Eps = 1e-6d;
        var result = new List<CadSketchEntity>();
        var n = angles.Count;

        // Find which CCW segment [angles[i] → angles[(i+1)%n]] contains clickAngle
        var clickInterval = n - 1;
        for (var i = 0; i < n - 1; i++)
        {
            if (clickAngle >= angles[i] - Eps && clickAngle < angles[i + 1] + Eps)
            {
                clickInterval = i;
                break;
            }
        }

        for (var i = 0; i < n; i++)
        {
            if (i == clickInterval) continue;
            var startA = angles[i];
            var endA = angles[(i + 1) % n];
            var sweep = endA > startA ? endA - startA : endA - startA + 360d;
            if (sweep < Eps) continue;
            result.Add(new CadSketchArc
            {
                CenterX = circle.CenterX,
                CenterY = circle.CenterY,
                Radius = circle.Radius,
                StartAngleDegrees = startA,
                EndAngleDegrees = endA,
                CounterClockwise = true
            });
        }
        return result;
    }

    private static double? LineLineIntersectT(CadSketchLine line1, CadSketchLine line2)
    {
        var d1x = line1.EndX - line1.StartX;
        var d1y = line1.EndY - line1.StartY;
        var d2x = line2.EndX - line2.StartX;
        var d2y = line2.EndY - line2.StartY;

        var cross = (d1x * d2y) - (d1y * d2x);
        if (Math.Abs(cross) < 1e-10d) return null;

        var ex = line2.StartX - line1.StartX;
        var ey = line2.StartY - line1.StartY;
        var t = ((ex * d2y) - (ey * d2x)) / cross;
        var s = ((ex * d1y) - (ey * d1x)) / cross;

        if (t < -1e-9d || t > 1d + 1e-9d) return null;
        if (s < -1e-9d || s > 1d + 1e-9d) return null;
        return Math.Clamp(t, 0d, 1d);
    }

    private static List<double> LineCircleIntersectT(CadSketchLine line, double cx, double cy, double r)
    {
        var ax = line.StartX - cx;
        var ay = line.StartY - cy;
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;

        var a = (dx * dx) + (dy * dy);
        if (a < 1e-12d) return [];
        var b = 2d * ((ax * dx) + (ay * dy));
        var c = (ax * ax) + (ay * ay) - (r * r);

        var disc = (b * b) - (4d * a * c);
        if (disc < 0d) return [];

        var sqrtDisc = Math.Sqrt(disc);
        var t1 = (-b - sqrtDisc) / (2d * a);
        var t2 = (-b + sqrtDisc) / (2d * a);

        var result = new List<double>();
        if (t1 >= -1e-9d && t1 <= 1d + 1e-9d) result.Add(Math.Clamp(t1, 0d, 1d));
        if (disc > 1e-10d && t2 >= -1e-9d && t2 <= 1d + 1e-9d) result.Add(Math.Clamp(t2, 0d, 1d));
        return result;
    }

    private static List<double> LineArcIntersectT(CadSketchLine line, CadSketchArc arc)
    {
        var result = new List<double>();
        foreach (var t in LineCircleIntersectT(line, arc.CenterX, arc.CenterY, arc.Radius))
        {
            var px = line.StartX + (t * (line.EndX - line.StartX));
            var py = line.StartY + (t * (line.EndY - line.StartY));
            var a = NormalizeAngle(Math.Atan2(py - arc.CenterY, px - arc.CenterX) * 180d / Math.PI);
            if (AngleLiesOnArc(arc, a))
                result.Add(t);
        }
        return result;
    }

    private static List<(double X, double Y)> CircleCircleIntersections(
        double cx1, double cy1, double r1, double cx2, double cy2, double r2)
    {
        var dx = cx2 - cx1;
        var dy = cy2 - cy1;
        var d = Math.Sqrt((dx * dx) + (dy * dy));
        if (d < 1e-10d || d > r1 + r2 + 1e-9d || d < Math.Abs(r1 - r2) - 1e-9d)
            return [];

        var a = ((r1 * r1) - (r2 * r2) + (d * d)) / (2d * d);
        var h2 = (r1 * r1) - (a * a);
        if (h2 < 0d) return [];
        var h = Math.Sqrt(h2);

        var mx = cx1 + (a * dx / d);
        var my = cy1 + (a * dy / d);
        if (h < 1e-10d)
            return [(mx, my)];
        return
        [
            (mx + (h * dy / d), my - (h * dx / d)),
            (mx - (h * dy / d), my + (h * dx / d)),
        ];
    }

    private static List<CadSketchEntity> BuildPolygonPreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingShapeAnchor is null)
        {
            return [];
        }

        var center = session.PendingShapeAnchor.Value;
        var radius = Distance(center, point);
        if (radius <= 0.001d)
        {
            return [];
        }

        return
        [
            new CadSketchPolygon
            {
                CenterX = center.X,
                CenterY = center.Y,
                Radius = radius,
                Sides = session.PendingPolygonSides
            }
        ];
    }

    private static List<CadSketchEntity> BuildSlotPreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingSketchPoints.Count == 0)
        {
            return [];
        }

        if (session.PendingSketchPoints.Count == 1)
        {
            var c1 = session.PendingSketchPoints[0];
            if (Distance(c1, point) <= 0.001d)
            {
                return [];
            }

            return [new CadSketchLine { StartX = c1.X, StartY = c1.Y, EndX = point.X, EndY = point.Y }];
        }

        var center1 = session.PendingSketchPoints[0];
        var center2 = session.PendingSketchPoints[1];
        var dx = center2.X - center1.X;
        var dy = center2.Y - center1.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        double radius;
        if (len < 0.001d)
        {
            radius = Distance(center1, point);
        }
        else
        {
            var nx = -dy / len;
            var ny = dx / len;
            var relX = point.X - center1.X;
            var relY = point.Y - center1.Y;
            radius = Math.Abs((relX * nx) + (relY * ny));
        }

        if (radius <= 0.001d)
        {
            return [];
        }

        return [new CadSketchSlot { Center1X = center1.X, Center1Y = center1.Y, Center2X = center2.X, Center2Y = center2.Y, Radius = radius }];
    }

    private static List<CadSketchEntity> BuildSplinePreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingSketchPoints.Count == 0)
        {
            return [];
        }

        var allPoints = session.PendingSketchPoints.Append(point).ToList();
        if (allPoints.Count < 2)
        {
            return [];
        }

        var spline = new CadSketchSpline();
        foreach (var p in allPoints)
        {
            spline.ControlPointsXY.Add(p.X);
            spline.ControlPointsXY.Add(p.Y);
        }

        return [spline];
    }

    private static List<CadSketchEntity> BuildOffsetPreview(CadSketchSession session, Vector2D point)
    {
        if (session.DraftEntities.Count == 0)
        {
            return [];
        }

        var distance = session.PendingOffsetDistance;
        var results = new List<CadSketchEntity>();
        foreach (var entity in session.DraftEntities)
        {
            switch (entity)
            {
                case CadSketchLine line:
                {
                    var dx = line.EndX - line.StartX;
                    var dy = line.EndY - line.StartY;
                    var len = Math.Sqrt((dx * dx) + (dy * dy));
                    if (len < 0.001d)
                    {
                        break;
                    }

                    var nx = -dy / len * distance;
                    var ny = dx / len * distance;
                    results.Add(new CadSketchLine
                    {
                        StartX = line.StartX + nx,
                        StartY = line.StartY + ny,
                        EndX = line.EndX + nx,
                        EndY = line.EndY + ny
                    });
                    break;
                }

                case CadSketchCircle circle:
                    results.Add(new CadSketchCircle
                    {
                        CenterX = circle.CenterX,
                        CenterY = circle.CenterY,
                        Radius = Math.Max(circle.Radius + distance, 0.2d)
                    });
                    break;
            }
        }

        return results;
    }

    private static List<CadSketchEntity> BuildFillet2dPreview(CadSketchSession session, Vector2D point)
    {
        var lines = session.DraftEntities.OfType<CadSketchLine>().ToList();
        if (lines.Count < 2)
        {
            return [];
        }

        if (!TryComputeFillet2d(lines[^2], lines[^1], session.PendingFilletRadius, out var arc))
        {
            return [];
        }

        return [arc];
    }

    private static List<CadSketchEntity> BuildMirrorPreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingShapeAnchor is null || session.DraftEntities.Count == 0)
            return [];

        var axisStart = session.PendingShapeAnchor.Value;
        if (Distance(axisStart, point) <= 0.001d)
            return [];

        var dx = point.X - axisStart.X;
        var dy = point.Y - axisStart.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        var dNormX = dx / len;
        var dNormY = dy / len;
        var axisAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

        var preview = new List<CadSketchEntity>
        {
            new CadSketchLine { StartX = axisStart.X, StartY = axisStart.Y, EndX = point.X, EndY = point.Y }
        };
        preview.AddRange(ReflectEntities(session.DraftEntities, axisStart.X, axisStart.Y, dNormX, dNormY, axisAngle));
        return preview;
    }

    private static CadSketchLine? CreatePlacedLine(CadSketchSession session, Vector2D clickPoint, out string? message)
    {
        if (session.PendingLineStart is null)
        {
            session.PendingLineStart = clickPoint;
            message = "Line start placed. Click again to create the segment.";
            return null;
        }

        var start = session.PendingLineStart.Value;
        var end = SnapLineEndpoint(start, clickPoint);

        var firstLine = session.DraftEntities.OfType<CadSketchLine>().FirstOrDefault();
        if (firstLine is not null &&
            session.DraftEntities.OfType<CadSketchLine>().Count() >= 2 &&
            Distance(end, new Vector2D(firstLine.StartX, firstLine.StartY)) <= 1.5d)
        {
            end = new Vector2D(firstLine.StartX, firstLine.StartY);
            session.PendingLineStart = null;
            message = "Added line segment and closed the loop.";
        }
        else
        {
            session.PendingLineStart = end;
            message = "Added line segment.";
        }

        if (Distance(start, end) <= 0.001d)
        {
            message = "Line segment ignored because start and end are the same point.";
            return null;
        }

        return new CadSketchLine
        {
            StartX = start.X,
            StartY = start.Y,
            EndX = end.X,
            EndY = end.Y
        };
    }

    private static Vector2D SnapLineEndpoint(Vector2D start, Vector2D rawEnd)
    {
        var dx = rawEnd.X - start.X;
        var dy = rawEnd.Y - start.Y;
        if (Math.Abs(dx) <= 1.25d)
        {
            return new Vector2D(start.X, rawEnd.Y);
        }

        if (Math.Abs(dy) <= 1.25d)
        {
            return new Vector2D(rawEnd.X, start.Y);
        }

        return rawEnd;
    }

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static int ComputeSketchDof(CadSketchSession session)
    {
        int totalDof = 0;
        foreach (var entity in session.DraftEntities)
        {
            totalDof += SketchEntityDof(entity);
        }

        int removedDof = 0;
        foreach (var constraint in session.ManualConstraints)
        {
            removedDof += SketchConstraintDofCost(constraint, session.DraftEntities);
        }

        foreach (var dimension in session.ManualDimensions.Where(item => !item.IsDriven))
        {
            removedDof += 1;
        }

        return totalDof - removedDof;
    }

    private static int SketchEntityDof(CadSketchEntity entity) => entity switch
    {
        CadSketchPoint => 2,
        CadSketchLine => 4,
        CadSketchRectangle => 4,
        CadSketchCircle => 3,
        CadSketchArc => 5,
        CadSketchSlot => 5,
        CadSketchPolygon => 4,
        CadSketchSpline spline => Math.Max(0, spline.ControlPointsXY.Count / 2) * 2,
        _ => 2
    };

    private static int SketchConstraintDofCost(CadSketchConstraint constraint, IReadOnlyList<CadSketchEntity> entities)
    {
        return constraint.Kind switch
        {
            CadSketchConstraintKind.Coincident => 2,
            CadSketchConstraintKind.Concentric => 2,
            CadSketchConstraintKind.Fixed => constraint.EntityIds
                .Select(id => entities.FirstOrDefault(entity => entity.Id == id))
                .Where(entity => entity is not null)
                .Sum(entity => SketchEntityDof(entity!)),
            CadSketchConstraintKind.Horizontal => 1,
            CadSketchConstraintKind.Vertical => 1,
            CadSketchConstraintKind.EqualRadius => 1,
            CadSketchConstraintKind.EqualLength => 1,
            CadSketchConstraintKind.Tangent => 1,
            CadSketchConstraintKind.Parallel => 1,
            CadSketchConstraintKind.Perpendicular => 1,
            _ => 0
        };
    }

    private static (double X, double Y) ReflectPoint(double px, double py, double ax, double ay, double dNormX, double dNormY)
    {
        var vx = px - ax;
        var vy = py - ay;
        var dot = vx * dNormX + vy * dNormY;
        var perpX = vx - dot * dNormX;
        var perpY = vy - dot * dNormY;
        return (px - 2 * perpX, py - 2 * perpY);
    }

    private static List<CadSketchEntity> ReflectEntities(
        IEnumerable<CadSketchEntity> entities,
        double ax, double ay,
        double dNormX, double dNormY,
        double axisAngleDegrees)
    {
        var result = new List<CadSketchEntity>();
        foreach (var entity in entities)
        {
            switch (entity)
            {
                case CadSketchLine line:
                    var (sx, sy) = ReflectPoint(line.StartX, line.StartY, ax, ay, dNormX, dNormY);
                    var (ex, ey) = ReflectPoint(line.EndX, line.EndY, ax, ay, dNormX, dNormY);
                    result.Add(new CadSketchLine { StartX = sx, StartY = sy, EndX = ex, EndY = ey });
                    break;
                case CadSketchCircle circle:
                    var (cx, cy) = ReflectPoint(circle.CenterX, circle.CenterY, ax, ay, dNormX, dNormY);
                    result.Add(new CadSketchCircle { CenterX = cx, CenterY = cy, Radius = circle.Radius });
                    break;
                case CadSketchArc arc:
                    var (acx, acy) = ReflectPoint(arc.CenterX, arc.CenterY, ax, ay, dNormX, dNormY);
                    result.Add(new CadSketchArc
                    {
                        CenterX = acx,
                        CenterY = acy,
                        Radius = arc.Radius,
                        StartAngleDegrees = NormalizeAngle(2.0 * axisAngleDegrees - arc.EndAngleDegrees),
                        EndAngleDegrees = NormalizeAngle(2.0 * axisAngleDegrees - arc.StartAngleDegrees),
                        CounterClockwise = arc.CounterClockwise
                    });
                    break;
                case CadSketchSlot slot:
                    var (s1x, s1y) = ReflectPoint(slot.Center1X, slot.Center1Y, ax, ay, dNormX, dNormY);
                    var (s2x, s2y) = ReflectPoint(slot.Center2X, slot.Center2Y, ax, ay, dNormX, dNormY);
                    result.Add(new CadSketchSlot { Center1X = s1x, Center1Y = s1y, Center2X = s2x, Center2Y = s2y, Radius = slot.Radius });
                    break;
                case CadSketchPolygon polygon:
                    var (pcx, pcy) = ReflectPoint(polygon.CenterX, polygon.CenterY, ax, ay, dNormX, dNormY);
                    result.Add(new CadSketchPolygon { CenterX = pcx, CenterY = pcy, Radius = polygon.Radius, Sides = polygon.Sides });
                    break;
                case CadSketchSpline spline:
                    var pts = new List<double>(spline.ControlPointsXY.Count);
                    for (var i = 0; i + 1 < spline.ControlPointsXY.Count; i += 2)
                    {
                        var (rx, ry) = ReflectPoint(spline.ControlPointsXY[i], spline.ControlPointsXY[i + 1], ax, ay, dNormX, dNormY);
                        pts.Add(rx);
                        pts.Add(ry);
                    }
                    result.Add(new CadSketchSpline { ControlPointsXY = pts });
                    break;
            }
        }
        return result;
    }

    private static bool TryCreateThreePointArc(Vector2D start, Vector2D end, Vector2D through, out CadSketchArc arc)
    {
        arc = new CadSketchArc();

        var determinant = 2d * (
            (start.X * (end.Y - through.Y)) +
            (end.X * (through.Y - start.Y)) +
            (through.X * (start.Y - end.Y)));

        if (Math.Abs(determinant) <= 0.0001d)
        {
            return false;
        }

        var startSq = (start.X * start.X) + (start.Y * start.Y);
        var endSq = (end.X * end.X) + (end.Y * end.Y);
        var throughSq = (through.X * through.X) + (through.Y * through.Y);

        var centerX = (
            (startSq * (end.Y - through.Y)) +
            (endSq * (through.Y - start.Y)) +
            (throughSq * (start.Y - end.Y))) / determinant;
        var centerY = (
            (startSq * (through.X - end.X)) +
            (endSq * (start.X - through.X)) +
            (throughSq * (end.X - start.X))) / determinant;

        var radius = Distance(new Vector2D(centerX, centerY), start);
        if (radius <= 0.001d)
        {
            return false;
        }

        var startAngle = Math.Atan2(start.Y - centerY, start.X - centerX) * 180d / Math.PI;
        var endAngle = Math.Atan2(end.Y - centerY, end.X - centerX) * 180d / Math.PI;
        var throughAngle = Math.Atan2(through.Y - centerY, through.X - centerX) * 180d / Math.PI;
        var counterClockwise = AngleLiesOnSweep(startAngle, endAngle, throughAngle);

        arc = new CadSketchArc
        {
            CenterX = centerX,
            CenterY = centerY,
            Radius = radius,
            StartAngleDegrees = startAngle,
            EndAngleDegrees = endAngle,
            CounterClockwise = counterClockwise
        };

        return true;
    }

    private static bool AngleLiesOnSweep(double startAngle, double endAngle, double testAngle)
    {
        var start = NormalizeAngle(startAngle);
        var end = NormalizeAngle(endAngle);
        var test = NormalizeAngle(testAngle);

        var ccwSweep = end - start;
        if (ccwSweep < 0d)
        {
            ccwSweep += 360d;
        }

        var ccwTest = test - start;
        if (ccwTest < 0d)
        {
            ccwTest += 360d;
        }

        return ccwTest <= ccwSweep;
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    /// <summary>
    /// Removes consecutive duplicate values within <paramref name="eps"/> from an already-sorted list.
    /// Used by all intersection-angle collectors to clean split parameters.
    /// </summary>
    private static List<double> DeduplicateAscending(List<double> sorted, double eps = 1e-9d)
    {
        var result = new List<double>(sorted.Count);
        foreach (var v in sorted)
            if (result.Count == 0 || v - result[^1] > eps)
                result.Add(v);
        return result;
    }

    /// <summary>
    /// Returns the normalized angle (0–360°) from <paramref name="cx"/>,<paramref name="cy"/>
    /// to point <paramref name="px"/>,<paramref name="py"/>.
    /// </summary>
    private static double PointToAngleDegrees(double px, double py, double cx, double cy) =>
        NormalizeAngle(Math.Atan2(py - cy, px - cx) * 180d / Math.PI);

    private static List<CadSketchEntity> NormalizeSketchEntities(IEnumerable<CadSketchEntity> entities)
    {
        var normalized = new List<CadSketchEntity>();
        foreach (var entity in entities)
        {
            if (entity is CadSketchRectangle rectangle)
            {
                normalized.AddRange(CreateRectangleLines(
                    rectangle.X,
                    rectangle.Y,
                    rectangle.X + rectangle.Width,
                    rectangle.Y + rectangle.Height,
                    rectangle.IsConstruction,
                    rectangle.IsFixed));
                continue;
            }

            normalized.Add(entity);
        }

        NormalizeLineChain(normalized);
        return normalized;
    }

    private static List<CadSketchEntity> BuildPreviewEntities(CadSketchSession session, Vector2D point)
    {
        return session.ActiveTool switch
        {
            CadSketchToolKind.Line => BuildLinePreview(session, point),
            CadSketchToolKind.Rectangle => BuildRectanglePreview(session, point),
            CadSketchToolKind.Circle => BuildCirclePreview(session, point),
            CadSketchToolKind.Arc => BuildArcPreview(session, point),
            CadSketchToolKind.Polygon => BuildPolygonPreview(session, point),
            CadSketchToolKind.Slot => BuildSlotPreview(session, point),
            CadSketchToolKind.Spline => BuildSplinePreview(session, point),
            CadSketchToolKind.Offset => BuildOffsetPreview(session, point),
            CadSketchToolKind.Fillet2d => BuildFillet2dPreview(session, point),
            CadSketchToolKind.Mirror => BuildMirrorPreview(session, point),
            _ => []
        };
    }

    private static List<CadSketchEntity> BuildLinePreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingLineStart is null)
        {
            return [];
        }

        var start = session.PendingLineStart.Value;
        var end = SnapLineEndpoint(start, point);

        var firstLine = session.DraftEntities.OfType<CadSketchLine>().FirstOrDefault();
        if (firstLine is not null &&
            session.DraftEntities.OfType<CadSketchLine>().Count() >= 2 &&
            Distance(end, new Vector2D(firstLine.StartX, firstLine.StartY)) <= 1.5d)
        {
            end = new Vector2D(firstLine.StartX, firstLine.StartY);
        }

        var entities = new List<CadSketchEntity>
        {
            new CadSketchPoint { X = start.X, Y = start.Y }
        };

        if (Distance(start, end) > 0.001d)
        {
            entities.Add(new CadSketchLine
            {
                StartX = start.X,
                StartY = start.Y,
                EndX = end.X,
                EndY = end.Y
            });
        }

        return entities;
    }

    private static List<CadSketchEntity> BuildRectanglePreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingShapeAnchor is null)
        {
            return [];
        }

        var anchor = session.PendingShapeAnchor.Value;
        if (Distance(anchor, point) <= 0.001d)
        {
            return [];
        }

        return CreatePlacedRectangleLines(
            new CadSketchSession { PendingShapeAnchor = anchor },
            point,
            out _)
            ?? [];
    }

    private static List<CadSketchEntity> BuildCirclePreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingShapeAnchor is null)
        {
            return [];
        }

        var center = session.PendingShapeAnchor.Value;
        var radius = Distance(center, point);
        if (radius <= 0.001d)
        {
            return [];
        }

        return
        [
            new CadSketchCircle
            {
                CenterX = center.X,
                CenterY = center.Y,
                Radius = radius
            }
        ];
    }

    private static List<CadSketchEntity> BuildArcPreview(CadSketchSession session, Vector2D point)
    {
        if (session.PendingSketchPoints.Count == 1)
        {
            var start = session.PendingSketchPoints[0];
            if (Distance(start, point) <= 0.001d)
            {
                return [];
            }

            return
            [
                new CadSketchLine
                {
                    StartX = start.X,
                    StartY = start.Y,
                    EndX = point.X,
                    EndY = point.Y
                }
            ];
        }

        if (session.PendingSketchPoints.Count != 2)
        {
            return [];
        }

        var arcStart = session.PendingSketchPoints[0];
        var arcEnd = session.PendingSketchPoints[1];
        if (!TryCreateThreePointArc(arcStart, arcEnd, point, out var arc))
        {
            return
            [
                new CadSketchLine
                {
                    StartX = arcStart.X,
                    StartY = arcStart.Y,
                    EndX = arcEnd.X,
                    EndY = arcEnd.Y
                },
                new CadSketchLine
                {
                    StartX = arcEnd.X,
                    StartY = arcEnd.Y,
                    EndX = point.X,
                    EndY = point.Y
                }
            ];
        }

        return [arc];
    }

    private static void NormalizeLineChain(IList<CadSketchEntity> entities)
    {
        if (entities.Count == 0 || entities.Any(entity => entity is not CadSketchLine))
        {
            return;
        }

        var lines = entities.Cast<CadSketchLine>().ToList();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (Math.Abs(line.EndY - line.StartY) <= 0.0001d)
            {
                line.EndY = line.StartY;
            }

            if (Math.Abs(line.EndX - line.StartX) <= 0.0001d)
            {
                line.EndX = line.StartX;
            }

            if (index == 0)
            {
                continue;
            }

            var previous = lines[index - 1];
            if (previous.IsConstruction == line.IsConstruction &&
                Distance(new Vector2D(previous.EndX, previous.EndY), new Vector2D(line.StartX, line.StartY)) <= 1.5d)
            {
                line.StartX = previous.EndX;
                line.StartY = previous.EndY;
            }
        }

        if (lines.Count < 3)
        {
            return;
        }

        var first = lines[0];
        var last = lines[^1];
        if (first.IsConstruction == last.IsConstruction &&
            Distance(new Vector2D(last.EndX, last.EndY), new Vector2D(first.StartX, first.StartY)) <= 1.5d)
        {
            last.EndX = first.StartX;
            last.EndY = first.StartY;
        }
    }

    private static List<CadSketchConstraint> InferConstraints(IReadOnlyList<CadSketchEntity> entities)
    {
        var probe = new SketchFeature
        {
            Entities = entities.ToList()
        };
        return probe.GetBasicConstraints().ToList();
    }

    private static void AddManualConstraint(CadSketchSession session, CadSketchConstraint constraint)
    {
        if (session.ManualConstraints.Any(existing => ConstraintKey(existing) == ConstraintKey(constraint)))
        {
            return;
        }

        session.ManualConstraints.Add(constraint);
    }

    private static void SolveSketchSession(CadSketchSession session, int iterations = 4)
    {
        if (session.DraftEntities.Count == 0)
        {
            return;
        }

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (var constraint in session.ManualConstraints)
            {
                ApplyConstraintToGeometry(session, constraint);
            }

            foreach (var dimension in session.ManualDimensions.Where(d => !d.IsDriven))
            {
                ApplyDimensionToGeometry(session, dimension);
            }
        }
    }

    private static void ApplyConstraintToGeometry(CadSketchSession session, CadSketchConstraint constraint)
    {
        var entities = constraint.EntityIds
            .Select(id => session.DraftEntities.FirstOrDefault(entity => entity.Id == id))
            .Where(entity => entity is not null)
            .Cast<CadSketchEntity>()
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        switch (constraint.Kind)
        {
            case CadSketchConstraintKind.Horizontal:
                if (entities[0] is CadSketchLine horizontalLine && !horizontalLine.IsFixed)
                {
                    ApplyHorizontalConstraint(session, horizontalLine);
                }
                break;

            case CadSketchConstraintKind.Vertical:
                if (entities[0] is CadSketchLine verticalLine && !verticalLine.IsFixed)
                {
                    ApplyVerticalConstraint(session, verticalLine);
                }
                break;

            case CadSketchConstraintKind.Coincident:
                ApplyCoincidentToEntities(entities);
                break;

            case CadSketchConstraintKind.EqualLength:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine referenceLine &&
                    entities[1] is CadSketchLine drivenLine &&
                    !drivenLine.IsFixed)
                {
                    SetLineLengthWithAnchor(session, drivenLine, LineLength(referenceLine));
                }
                break;

            case CadSketchConstraintKind.EqualRadius:
                if (entities.Count >= 2)
                {
                    var radius = TryGetRadius(entities[0]);
                    if (radius > 0d)
                    {
                        TrySetRadius(entities[1], radius);
                    }
                }
                break;

            case CadSketchConstraintKind.Fixed:
                break;

            case CadSketchConstraintKind.Parallel:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine parallelReference &&
                    entities[1] is CadSketchLine parallelDriven &&
                    !parallelDriven.IsFixed)
                {
                    MatchLineDirection(session, parallelReference, parallelDriven, perpendicular: false);
                }
                break;

            case CadSketchConstraintKind.Perpendicular:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine perpReference &&
                    entities[1] is CadSketchLine perpDriven &&
                    !perpDriven.IsFixed)
                {
                    MatchLineDirection(session, perpReference, perpDriven, perpendicular: true);
                }
                break;

            case CadSketchConstraintKind.Concentric:
                if (entities.Count >= 2 &&
                    TryGetCenter(entities[0], out var refCenter) &&
                    TryGetCenter(entities[1], out _) &&
                    !entities[1].IsFixed)
                {
                    TrySetCenter(entities[1], refCenter.X, refCenter.Y);
                }
                break;
        }
    }

    private static void ApplyDimensionToGeometry(CadSketchSession session, CadSketchDimension dimension)
    {
        if (dimension.EntityIds.Count == 1)
        {
            var entity = session.DraftEntities.FirstOrDefault(item => item.Id == dimension.EntityIds[0]);
            if (entity is null || entity.IsFixed)
            {
                return;
            }

            if (entity is CadSketchLine line && string.Equals(dimension.ParameterKey, "Length", StringComparison.OrdinalIgnoreCase))
            {
                SetLineLengthWithAnchor(session, line, dimension.Value);
                return;
            }

            entity.TrySetParameter(dimension.ParameterKey ?? string.Empty, dimension.Value);
            return;
        }

        if (dimension.Kind == CadSketchDimensionKind.Angle && dimension.EntityIds.Count >= 2)
        {
            var lineA = session.DraftEntities.OfType<CadSketchLine>().FirstOrDefault(item => item.Id == dimension.EntityIds[0]);
            var lineB = session.DraftEntities.OfType<CadSketchLine>().FirstOrDefault(item => item.Id == dimension.EntityIds[1]);
            if (lineA is null || lineB is null || lineB.IsFixed)
            {
                return;
            }

            if (!TryLineIntersection2D(lineA, lineB, out var ix, out var iy))
            {
                return;
            }

            var baseAngle = Math.Atan2(lineA.EndY - lineA.StartY, lineA.EndX - lineA.StartX);
            var targetAngle = baseAngle + (dimension.Value * Math.PI / 180.0);
            var lenB = LineLength(lineB);
            SetLineDirectionAroundAnchor(session, lineB, targetAngle, ix, iy, lenB);
        }
    }

    private static void ApplyCoincidentToEntities(IReadOnlyList<CadSketchEntity> entities)
    {
        if (entities.Count < 2)
        {
            return;
        }

        var reference = entities[0];
        var driven = entities[1];
        if (driven.IsFixed)
        {
            return;
        }

        if (reference is CadSketchLine referenceLine && driven is CadSketchLine drivenLine)
        {
            var referenceEndpoints = GetLineEndpoints(referenceLine);
            var drivenEndpoints = GetLineEndpoints(drivenLine);
            var referenceCandidates = new[] { referenceEndpoints.Start, referenceEndpoints.End };
            var drivenCandidates = new[] { drivenEndpoints.Start, drivenEndpoints.End };

            var bestDistance = double.MaxValue;
            var bestReferenceIndex = 0;
            var bestDrivenIndex = 0;
            for (var referenceIndex = 0; referenceIndex < 2; referenceIndex++)
            {
                for (var drivenIndex = 0; drivenIndex < 2; drivenIndex++)
                {
                    var distance = Distance(referenceCandidates[referenceIndex], drivenCandidates[drivenIndex]);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestReferenceIndex = referenceIndex;
                        bestDrivenIndex = drivenIndex;
                    }
                }
            }

            SetLineEndpoint(drivenLine, bestDrivenIndex, referenceCandidates[bestReferenceIndex]);
            return;
        }

        if (reference is CadSketchPoint referencePoint && driven is CadSketchLine drivenLineFromPoint)
        {
            SnapLineEndpointToTarget(drivenLineFromPoint, new Vector2D(referencePoint.X, referencePoint.Y));
            return;
        }

        if (reference is CadSketchLine referenceLineToPoint && driven is CadSketchPoint drivenPoint)
        {
            var endpoints = GetLineEndpoints(referenceLineToPoint);
            var startDistance = Distance(new Vector2D(drivenPoint.X, drivenPoint.Y), endpoints.Start);
            var endDistance = Distance(new Vector2D(drivenPoint.X, drivenPoint.Y), endpoints.End);
            var target = startDistance <= endDistance ? endpoints.Start : endpoints.End;
            drivenPoint.X = target.X;
            drivenPoint.Y = target.Y;
            return;
        }

        if (reference is CadSketchPoint sourcePoint && driven is CadSketchPoint targetPoint)
        {
            targetPoint.X = sourcePoint.X;
            targetPoint.Y = sourcePoint.Y;
            return;
        }

        if (TryGetCenter(reference, out var center))
        {
            if (driven is CadSketchPoint point)
            {
                point.X = center.X;
                point.Y = center.Y;
                return;
            }

            TrySetCenter(driven, center.X, center.Y);
        }
    }

    private static (Vector2D Start, Vector2D End) GetLineEndpoints(CadSketchLine line)
        => (new Vector2D(line.StartX, line.StartY), new Vector2D(line.EndX, line.EndY));

    private static void SetLineEndpoint(CadSketchLine line, int endpointIndex, Vector2D value)
    {
        if (endpointIndex == 0)
        {
            line.StartX = value.X;
            line.StartY = value.Y;
            return;
        }

        line.EndX = value.X;
        line.EndY = value.Y;
    }

    private static void SetLineLengthPreserveStart(CadSketchLine line, double targetLength)
    {
        var currentLength = LineLength(line);
        if (currentLength < 1e-9d)
        {
            return;
        }

        var safeLength = Math.Max(Math.Abs(targetLength), 0.001d);
        var ux = (line.EndX - line.StartX) / currentLength;
        var uy = (line.EndY - line.StartY) / currentLength;
        line.EndX = line.StartX + ux * safeLength;
        line.EndY = line.StartY + uy * safeLength;
    }

    private static void SetLineLengthWithAnchor(CadSketchSession session, CadSketchLine line, double targetLength)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(session, line);
        if (anchorIndex == 0)
        {
            SetLineLengthPreserveStart(line, targetLength);
            return;
        }

        var reversed = new CadSketchLine
        {
            StartX = line.EndX,
            StartY = line.EndY,
            EndX = line.StartX,
            EndY = line.StartY
        };
        SetLineLengthPreserveStart(reversed, targetLength);
        line.StartX = reversed.EndX;
        line.StartY = reversed.EndY;
        line.EndX = reversed.StartX;
        line.EndY = reversed.StartY;
    }

    private static void MatchLineDirection(CadSketchSession session, CadSketchLine reference, CadSketchLine driven, bool perpendicular)
    {
        var refLen = LineLength(reference);
        var drivenLen = LineLength(driven);
        if (refLen < 1e-9d || drivenLen < 1e-9d)
        {
            return;
        }

        var ux = (reference.EndX - reference.StartX) / refLen;
        var uy = (reference.EndY - reference.StartY) / refLen;
        if (perpendicular)
        {
            (ux, uy) = (-uy, ux);
        }

        var anchorIndex = GetPreferredLineAnchorIndex(session, driven);
        if (anchorIndex == 0)
        {
            driven.EndX = driven.StartX + ux * drivenLen;
            driven.EndY = driven.StartY + uy * drivenLen;
        }
        else
        {
            driven.StartX = driven.EndX - ux * drivenLen;
            driven.StartY = driven.EndY - uy * drivenLen;
        }
    }

    private static void ApplyHorizontalConstraint(CadSketchSession session, CadSketchLine line)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(session, line);
        if (anchorIndex == 0)
        {
            line.EndY = line.StartY;
        }
        else
        {
            line.StartY = line.EndY;
        }
    }

    private static void ApplyVerticalConstraint(CadSketchSession session, CadSketchLine line)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(session, line);
        if (anchorIndex == 0)
        {
            line.EndX = line.StartX;
        }
        else
        {
            line.StartX = line.EndX;
        }
    }

    private static void SetLineDirectionAroundAnchor(CadSketchSession session, CadSketchLine line, double targetAngle, double anchorX, double anchorY, double length)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(session, line, new Vector2D(anchorX, anchorY));
        var dx = Math.Cos(targetAngle) * length;
        var dy = Math.Sin(targetAngle) * length;
        if (anchorIndex == 0)
        {
            line.StartX = anchorX;
            line.StartY = anchorY;
            line.EndX = anchorX + dx;
            line.EndY = anchorY + dy;
        }
        else
        {
            line.EndX = anchorX;
            line.EndY = anchorY;
            line.StartX = anchorX - dx;
            line.StartY = anchorY - dy;
        }
    }

    private static void SnapLineEndpointToTarget(CadSketchLine line, Vector2D target)
    {
        var endpoints = GetLineEndpoints(line);
        var startDistance = Distance(endpoints.Start, target);
        var endDistance = Distance(endpoints.End, target);
        SetLineEndpoint(line, startDistance <= endDistance ? 0 : 1, target);
    }

    private static int GetPreferredLineAnchorIndex(CadSketchSession session, CadSketchLine line, Vector2D? preferredAnchor = null)
    {
        if (preferredAnchor is Vector2D anchor)
        {
            var endpoints = GetLineEndpoints(line);
            return Distance(endpoints.Start, anchor) <= Distance(endpoints.End, anchor) ? 0 : 1;
        }

        var endpointsForLine = GetLineEndpoints(line);
        foreach (var constraint in session.ManualConstraints.Where(item =>
                     item.Kind == CadSketchConstraintKind.Coincident &&
                     item.EntityIds.Contains(line.Id)))
        {
            foreach (var entityId in constraint.EntityIds)
            {
                if (entityId == line.Id)
                {
                    continue;
                }

                var target = session.DraftEntities.FirstOrDefault(item => item.Id == entityId);
                if (target is null || !TryGetCenter(target, out var center))
                {
                    continue;
                }

                return Distance(endpointsForLine.Start, center) <= Distance(endpointsForLine.End, center) ? 0 : 1;
            }
        }

        return 0;
    }

    private static double TryGetRadius(CadSketchEntity entity) => entity switch
    {
        CadSketchCircle circle => circle.Radius,
        CadSketchArc arc => arc.Radius,
        _ => 0d
    };

    private static bool TrySetRadius(CadSketchEntity entity, double radius)
    {
        switch (entity)
        {
            case CadSketchCircle circle when !circle.IsFixed:
                circle.Radius = Math.Max(Math.Abs(radius), 0.2d);
                return true;
            case CadSketchArc arc when !arc.IsFixed:
                arc.Radius = Math.Max(Math.Abs(radius), 0.2d);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetCenter(CadSketchEntity entity, out Vector2D center)
    {
        switch (entity)
        {
            case CadSketchPoint point:
                center = new Vector2D(point.X, point.Y);
                return true;
            case CadSketchCircle circle:
                center = new Vector2D(circle.CenterX, circle.CenterY);
                return true;
            case CadSketchArc arc:
                center = new Vector2D(arc.CenterX, arc.CenterY);
                return true;
            default:
                center = default;
                return false;
        }
    }

    private static bool TrySetCenter(CadSketchEntity entity, double x, double y)
    {
        switch (entity)
        {
            case CadSketchPoint point when !point.IsFixed:
                point.X = x;
                point.Y = y;
                return true;
            case CadSketchCircle circle when !circle.IsFixed:
                circle.CenterX = x;
                circle.CenterY = y;
                return true;
            case CadSketchArc arc when !arc.IsFixed:
                arc.CenterX = x;
                arc.CenterY = y;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Merges inferred dimensions with manual ones, preferring manual over inferred
    /// for the same entity+kind pair to avoid duplicate entries in the panel.
    /// </summary>
    private static List<CadSketchDimension> MergeSketchDimensions(
        IEnumerable<CadSketchDimension> inferred,
        IEnumerable<CadSketchDimension> manual)
    {
        var manualList = manual.ToList();
        // Build a set of (entityId, kind) covered by manual dims
        var manualCovered = new HashSet<(Guid, CadSketchDimensionKind)>(
            manualList.SelectMany(d => d.EntityIds.Select(id => (id, d.Kind))));

        var merged = new List<CadSketchDimension>();
        // Only include inferred dims for entity+kind pairs not covered by a manual dim
        foreach (var dim in inferred)
        {
            if (!dim.EntityIds.Any(id => manualCovered.Contains((id, dim.Kind))))
                merged.Add(dim);
        }
        merged.AddRange(manualList);
        return merged;
    }

    private static List<CadSketchConstraint> MergeSketchConstraints(
        IEnumerable<CadSketchConstraint> inferred,
        IEnumerable<CadSketchConstraint> manual)
    {
        var merged = new List<CadSketchConstraint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var constraint in inferred.Concat(manual))
        {
            if (seen.Add(ConstraintKey(constraint)))
            {
                merged.Add(constraint);
            }
        }

        return merged;
    }

    private static string ConstraintKey(CadSketchConstraint constraint)
    {
        var ids = constraint.EntityIds
            .Select(id => id.ToString("N"))
            .OrderBy(id => id, StringComparer.Ordinal);
        return $"{constraint.Kind}:{string.Join(",", ids)}";
    }

    private static double LineLength(CadSketchLine line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static List<CadSketchDimension> InferDimensions(IReadOnlyList<CadSketchEntity> entities)
    {
        var probe = new SketchFeature
        {
            Entities = entities.ToList()
        };
        return probe.GetBasicDimensions().ToList();
    }

    private static IEnumerable<CadSketchLine> CreateRectangleLines(
        double minX,
        double minY,
        double maxX,
        double maxY,
        bool isConstruction = false,
        bool isFixed = false)
    {
        yield return new CadSketchLine
        {
            StartX = minX,
            StartY = minY,
            EndX = maxX,
            EndY = minY,
            IsConstruction = isConstruction,
            IsFixed = isFixed
        };
        yield return new CadSketchLine
        {
            StartX = maxX,
            StartY = minY,
            EndX = maxX,
            EndY = maxY,
            IsConstruction = isConstruction,
            IsFixed = isFixed
        };
        yield return new CadSketchLine
        {
            StartX = maxX,
            StartY = maxY,
            EndX = minX,
            EndY = maxY,
            IsConstruction = isConstruction,
            IsFixed = isFixed
        };
        yield return new CadSketchLine
        {
            StartX = minX,
            StartY = maxY,
            EndX = minX,
            EndY = minY,
            IsConstruction = isConstruction,
            IsFixed = isFixed
        };
    }

    private static PrimitiveFeature CreatePrimitiveFeature(CadPrimitiveKind primitiveKind, string name)
    {
        return primitiveKind switch
        {
            CadPrimitiveKind.Box => new BoxFeature { Name = name },
            CadPrimitiveKind.Cylinder => new CylinderFeature { Name = name },
            CadPrimitiveKind.Sphere => new SphereFeature { Name = name },
            CadPrimitiveKind.Cone => new ConeFeature { Name = name },
            CadPrimitiveKind.Torus => new TorusFeature { Name = name },
            CadPrimitiveKind.Pyramid => new PyramidFeature { Name = name },
            CadPrimitiveKind.Wedge => new WedgeFeature { Name = name },
            CadPrimitiveKind.Ellipsoid => new EllipsoidFeature { Name = name },
            CadPrimitiveKind.Capsule => new CapsuleFeature { Name = name },
            CadPrimitiveKind.Hemisphere => new HemisphereFeature { Name = name },
            CadPrimitiveKind.Prism => new PrismFeature { Name = name },
            CadPrimitiveKind.Arrow => new ArrowFeature { Name = name },
            CadPrimitiveKind.Icosphere => new IcosphereFeature { Name = name },
            CadPrimitiveKind.Tetrahedron => new TetrahedronFeature { Name = name },
            CadPrimitiveKind.Octahedron => new OctahedronFeature { Name = name },
            CadPrimitiveKind.Icosahedron => new IcosahedronFeature { Name = name },
            _ => throw new ArgumentOutOfRangeException(nameof(primitiveKind), primitiveKind, "Unsupported primitive.")
        };
    }

    private CadActionResult Success(string message, bool mutated)
    {
        return CadActionResult.Success(message, mutated, Compile());
    }

    private CadActionResult Failure(string message)
    {
        return CadActionResult.Failure(message, Compile());
    }

    private CadActionResult Unsupported(string message)
    {
        return CadActionResult.Failure(message, Compile());
    }

    public bool TryComputeExtrudePreviewSolid(double distance, bool reverseDirection, out Solid solid, out string failureMessage)
    {
        solid = new BoxSolid(1d, 1d, 1d);
        var selection = ResolveSelectedSketchSelection();
        if (selection.Sketch is null)
        {
            failureMessage = "No sketch selected.";
            return false;
        }

        var sketch = selection.Sketch;
        var tempFeature = new ExtrudeFeature
        {
            Id = Guid.NewGuid(),
            Name = "Preview",
            SketchFeatureId = sketch.Id,
            Depth = Math.Max(0.2d, Math.Abs(distance)),
            ReverseDirection = reverseDirection,
            Symmetric = false,
            Operation = CadExtrudeOperation.NewBody,
            TargetBodyId = Guid.Empty
        };

        if (!sketch.IsClosedProfile)
        {
            failureMessage = "Profile is not closed.";
            return false;
        }

        return CadProjectCompiler.TryComputeExtrudeSolid(sketch, tempFeature, out solid, out failureMessage);
    }
}

public sealed class CadProjectCompiler
{
    public CadCompileResult Compile(CadProject project)
    {
        project.Scene.EnsureReferencePlanes();

        var compiledBodies = new List<CadCompiledBody>();
        var compiledSketches = new List<CadCompiledSketch>();
        var diagnostics = new List<CadCompileDiagnostic>();

        foreach (var body in project.Scene.Bodies)
        {
            CompileBody(body, project.Scene.Bodies, compiledBodies, compiledSketches, diagnostics);
        }

        return new CadCompileResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Units = project.Units,
            Mode = project.ActiveMode,
            ActivePlaneName = ResolveActivePlaneName(project),
            Selection = project.Selection,
            Planes = project.Scene.ReferencePlanes
                .Select(plane => new CadCompiledPlane
                {
                    PlaneId = plane.Id,
                    Name = plane.Name,
                    Kind = plane.Kind,
                    Visible = plane.Visible
                })
                .ToList(),
            Bodies = compiledBodies,
            Sketches = compiledSketches,
            Diagnostics = diagnostics
        };
    }

    private static SketchFeature? FindSketchInBodies(IEnumerable<CadBody> bodies, Guid sketchFeatureId)
        => bodies.SelectMany(b => b.Features.OfType<SketchFeature>()).FirstOrDefault(s => s.Id == sketchFeatureId);

    private static void CompileBody(
        CadBody body,
        IReadOnlyList<CadBody> allBodies,
        ICollection<CadCompiledBody> compiledBodies,
        ICollection<CadCompiledSketch> compiledSketches,
        ICollection<CadCompileDiagnostic> diagnostics)
    {
        Solid? current = null;
        SketchFeature? lastSketch = null;
        CadFeatureKind sourceKind = CadFeatureKind.Box;
        var featureIds = new List<Guid>();

        foreach (var feature in body.Features.Where(feature => !feature.Suppressed))
        {
            switch (feature)
            {
                case PrimitiveFeature primitive when current is null:
                    current = primitive.CreateSolid() with { Name = body.Name };
                    sourceKind = primitive.Kind;
                    featureIds.Add(feature.Id);
                    break;

                case PrimitiveFeature primitive:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Body {body.Name} contains an additional create feature {primitive.Name} that is ignored in this slice.",
                        body.Id,
                        primitive.Id));
                    break;

                case MoveFeature move:
                    if (current is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Move feature {move.Name} has no base solid in body {body.Name}.",
                            body.Id,
                            move.Id));
                        break;
                    }

                    current = move.Apply(current) with { Name = body.Name };
                    sourceKind = move.Kind;
                    featureIds.Add(move.Id);
                    break;

                case SketchFeature sketch:
                    lastSketch = sketch;
                    compiledSketches.Add(new CadCompiledSketch
                    {
                        BodyId = body.Id,
                        SketchId = sketch.Id,
                        BodyName = body.Name,
                        Name = sketch.Name,
                        PlaneName = sketch.PlaneName,
                        EntityCount = sketch.Entities.Count,
                        IsClosedProfile = sketch.IsClosedProfile
                    });
                    break;

                case ExtrudeFeature extrude when current is null:
                {
                    var targetSketch = body.Features
                        .OfType<SketchFeature>()
                        .FirstOrDefault(item => item.Id == extrude.SketchFeatureId)
                        ?? lastSketch;

                    if (targetSketch is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Extrude feature {extrude.Name} has no sketch source in body {body.Name}.",
                            body.Id,
                            extrude.Id));
                        break;
                    }

                    if (!TryCreateExtrudedSolid(targetSketch, extrude, out var extrudedSolid, out var failureMessage))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            failureMessage,
                            body.Id,
                            extrude.Id));
                        break;
                    }

                    current = extrudedSolid with { Name = body.Name };
                    sourceKind = extrude.Kind;
                    featureIds.Add(targetSketch.Id);
                    featureIds.Add(extrude.Id);
                    break;
                }

                case ExtrudeFeature extrude when current is not null:
                {
                    if (extrude.Operation is not CadExtrudeOperation.Join and not CadExtrudeOperation.Cut)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Body {body.Name} contains an additional extrude feature {extrude.Name} that is ignored in this slice.",
                            body.Id,
                            extrude.Id));
                        break;
                    }

                    var targetSketch = body.Features
                        .OfType<SketchFeature>()
                        .FirstOrDefault(item => item.Id == extrude.SketchFeatureId)
                        ?? lastSketch;

                    if (targetSketch is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Extrude feature {extrude.Name} has no sketch source in body {body.Name}.",
                            body.Id,
                            extrude.Id));
                        break;
                    }

                    if (!TryCreateExtrudedSolid(targetSketch, extrude, out var operationTool, out var operationFailure))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            operationFailure,
                            body.Id,
                            extrude.Id));
                        break;
                    }

                    if (extrude.Operation == CadExtrudeOperation.Join)
                    {
                        current = new BooleanSolid(BooleanOperation.Union, current, operationTool) { Name = body.Name };
                    }
                    else
                    {
                        current = new BooleanSolid(BooleanOperation.Subtract, current, operationTool) { Name = body.Name };
                    }

                    featureIds.Add(targetSketch.Id);
                    featureIds.Add(extrude.Id);
                    break;
                }

                case RevolveFeature revolve when current is null:
                {
                    var targetSketch = body.Features
                        .OfType<SketchFeature>()
                        .FirstOrDefault(item => item.Id == revolve.SketchFeatureId)
                        ?? lastSketch;

                    if (targetSketch is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Revolve feature {revolve.Name} has no sketch source in body {body.Name}.",
                            body.Id,
                            revolve.Id));
                        break;
                    }

                    if (!ProfileBuilder.TryBuild(targetSketch, out var profile, out var failMsg))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            failMsg,
                            body.Id,
                            revolve.Id));
                        break;
                    }

                    var axis = revolve.Axis is CadAxis.X ? ProfileAxis.X : ProfileAxis.Y;
                    var radiusCoordinates = axis == ProfileAxis.X
                        ? profile.Points.Select(point => point.Y).ToList()
                        : profile.Points.Select(point => point.X).ToList();
                    var hasPositive = radiusCoordinates.Any(value => value > 0.0001d);
                    var hasNegative = radiusCoordinates.Any(value => value < -0.0001d);
                    if ((hasPositive && hasNegative) || (!hasPositive && !hasNegative))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            (hasPositive && hasNegative)
                                ? (axis == ProfileAxis.X
                                    ? "Profile crosses the selected revolve axis. Keep all profile points on one side of the local X axis."
                                    : "Profile crosses the selected revolve axis. Keep all profile points on one side of the local Y axis.")
                                : "Profile collapses onto the revolve axis.",
                            body.Id,
                            revolve.Id));
                        break;
                    }

                    current = new RevolveSolid(profile, revolve.AngleDegrees)
                    {
                        Name = body.Name,
                        Plane = ProfileBuilder.GetOrientation(targetSketch.PlaneName),
                        Axis = axis
                    };
                    sourceKind = revolve.Kind;
                    featureIds.Add(targetSketch.Id);
                    featureIds.Add(revolve.Id);
                    break;
                }

                case RevolveFeature revolve:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Body {body.Name} contains an additional revolve feature {revolve.Name} that is ignored.",
                        body.Id,
                        revolve.Id));
                    break;

                case SweepFeature sweep when current is null:
                {
                    var targetSketch = body.Features
                        .OfType<SketchFeature>()
                        .FirstOrDefault(item => item.Id == sweep.SketchFeatureId)
                        ?? lastSketch;

                    if (targetSketch is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Sweep feature {sweep.Name} has no sketch source in body {body.Name}.",
                            body.Id,
                            sweep.Id));
                        break;
                    }

                    if (!ProfileBuilder.TryBuild(targetSketch, out var profile, out var failMsg))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            failMsg,
                            body.Id,
                            sweep.Id));
                        break;
                    }

                    current = new SweepSolid(profile, sweep.Distance)
                    {
                        Name = body.Name,
                        Plane = ProfileBuilder.GetOrientation(targetSketch.PlaneName),
                        TwistDegrees = sweep.TwistDegrees
                    };
                    sourceKind = sweep.Kind;
                    featureIds.Add(targetSketch.Id);
                    featureIds.Add(sweep.Id);
                    break;
                }

                case SweepFeature sweep:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Body {body.Name} contains an additional sweep feature {sweep.Name} that is ignored.",
                        body.Id,
                        sweep.Id));
                    break;

                case LoftFeature loft when current is null:
                {
                    var sketchA = FindSketchInBodies(allBodies, loft.ProfileASketchId);
                    var sketchB = FindSketchInBodies(allBodies, loft.ProfileBSketchId);

                    if (sketchA is null || sketchB is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Loft feature {loft.Name} cannot find one or both sketch profiles.",
                            body.Id,
                            loft.Id));
                        break;
                    }

                    if (!ProfileBuilder.TryBuild(sketchA, out var pA, out var eA))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(CadDiagnosticSeverity.Warning, eA, body.Id, loft.Id));
                        break;
                    }

                    if (!ProfileBuilder.TryBuild(sketchB, out var pB, out var eB))
                    {
                        diagnostics.Add(new CadCompileDiagnostic(CadDiagnosticSeverity.Warning, eB, body.Id, loft.Id));
                        break;
                    }

                    current = new LoftSolid(pA, pB, loft.Distance)
                    {
                        Name = body.Name,
                        Plane = ProfileBuilder.GetOrientation(sketchA.PlaneName)
                    };
                    sourceKind = loft.Kind;
                    featureIds.Add(loft.Id);
                    break;
                }

                case LoftFeature loft:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Body {body.Name} contains an additional loft feature {loft.Name} that is ignored.",
                        body.Id,
                        loft.Id));
                    break;

                case FilletFeature fillet when current is ExtrudeSolid ex:
                    current = ex with { FilletRadius = fillet.Radius, Name = body.Name };
                    featureIds.Add(fillet.Id);
                    break;

                case FilletFeature fillet:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Fillet feature {fillet.Name} requires an extrude base solid in body {body.Name}.",
                        body.Id,
                        fillet.Id));
                    break;

                case ChamferFeature chamfer when current is ExtrudeSolid exC:
                    current = exC with { ChamferDistance = chamfer.Distance, Name = body.Name };
                    featureIds.Add(chamfer.Id);
                    break;

                case ChamferFeature chamfer:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Chamfer feature {chamfer.Name} requires an extrude base solid in body {body.Name}.",
                        body.Id,
                        chamfer.Id));
                    break;

                case ShellFeature shell when current is ExtrudeSolid ex:
                    current = ex with { ShellThickness = shell.Thickness, Name = body.Name };
                    featureIds.Add(shell.Id);
                    break;

                case ShellFeature shell:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Shell feature {shell.Name} requires an extrude base solid in body {body.Name}.",
                        body.Id,
                        shell.Id));
                    break;

                case MirrorFeature mirror when current is not null:
                    current = new MirrorSolid(current, mirror.Axis) { Name = body.Name };
                    featureIds.Add(mirror.Id);
                    break;

                case MirrorFeature mirror:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Mirror feature {mirror.Name} has no base solid in body {body.Name}.",
                        body.Id,
                        mirror.Id));
                    break;

                case LinearPatternFeature lp when current is not null:
                {
                    var (dx, dy, dz) = lp.Axis switch
                    {
                        CadAxis.Y => (0d, lp.Spacing, 0d),
                        CadAxis.Z => (0d, 0d, lp.Spacing),
                        _ => (lp.Spacing, 0d, 0d)
                    };
                    current = new LinearPatternSolid(current, lp.Count, dx, dy, dz) { Name = body.Name };
                    featureIds.Add(lp.Id);
                    break;
                }

                case LinearPatternFeature lp:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Linear pattern feature {lp.Name} has no base solid in body {body.Name}.",
                        body.Id,
                        lp.Id));
                    break;

                case CircularPatternFeature cp when current is not null:
                {
                    var cpAxis = cp.Axis switch
                    {
                        CadAxis.X => MirrorAxis.X,
                        CadAxis.Z => MirrorAxis.Z,
                        _ => MirrorAxis.Y
                    };
                    current = new CircularPatternSolid(current, cp.Count, cp.TotalAngle, cpAxis) { Name = body.Name };
                    featureIds.Add(cp.Id);
                    break;
                }

                case CircularPatternFeature cp:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Circular pattern feature {cp.Name} has no base solid in body {body.Name}.",
                        body.Id,
                        cp.Id));
                    break;

                case BooleanFeature boolean when current is null:
                {
                    var solidA = compiledBodies.FirstOrDefault(b => b.BodyId == boolean.BodyAId)?.Solid;
                    var solidB = compiledBodies.FirstOrDefault(b => b.BodyId == boolean.BodyBId)?.Solid;

                    if (solidA is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Boolean feature {boolean.Name} could not resolve Body A '{boolean.BodyAName}'.",
                            body.Id,
                            boolean.Id));
                        break;
                    }

                    if (solidB is null)
                    {
                        diagnostics.Add(new CadCompileDiagnostic(
                            CadDiagnosticSeverity.Warning,
                            $"Boolean feature {boolean.Name} could not resolve Body B '{boolean.BodyBName}'.",
                            body.Id,
                            boolean.Id));
                        break;
                    }

                    current = new BooleanSolid(boolean.Operation, solidA, solidB) { Name = body.Name };
                    sourceKind = CadFeatureKind.BooleanBody;
                    featureIds.Add(boolean.Id);
                    break;
                }

                case BooleanFeature boolean:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Boolean feature {boolean.Name} cannot be applied to body {body.Name} because it already has a base solid.",
                        body.Id,
                        boolean.Id));
                    break;

                // region: task 29 start
                case HoleFeature hole when current is not null:
                {
                    var holeRadius = hole.Diameter / 2.0;
                    var holeHeight = hole.DepthKind == HoleDepthKind.ThroughAll
                        ? 10000.0
                        : hole.DepthValue * 2.0;
                    Solid holeCylinder = new CylinderSolid(holeRadius, holeHeight);
                    if (Math.Abs(hole.CenterOffsetX) > 0.0001 || Math.Abs(hole.CenterOffsetY) > 0.0001)
                    {
                        holeCylinder = new TransformedSolid(holeCylinder,
                            Transform3D.CreateTranslation(hole.CenterOffsetX, hole.CenterOffsetY, 0));
                    }
                    current = new BooleanSolid(BooleanOperation.Subtract, current, holeCylinder) { Name = body.Name };
                    featureIds.Add(hole.Id);
                    break;
                }

                case HoleFeature hole:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Hole feature {hole.Name} has no base solid in body {body.Name}.",
                        body.Id,
                        hole.Id));
                    break;
                // region: task 29 end

                case PlaceholderFeature:
                    break;

                default:
                    diagnostics.Add(new CadCompileDiagnostic(
                        CadDiagnosticSeverity.Warning,
                        $"Unsupported feature {feature.Name} on body {body.Name}.",
                        body.Id,
                        feature.Id));
                    break;
            }
        }

        if (current is null)
        {
            return;
        }

        compiledBodies.Add(new CadCompiledBody
        {
            BodyId = body.Id,
            Name = body.Name,
            SourceKind = sourceKind,
            Solid = current,
            FeatureIds = featureIds,
            Parameters = body.Features.SelectMany(feature => feature.GetParameters()).ToList()
        });
    }

    public static bool TryComputeExtrudeSolid(SketchFeature sketch, ExtrudeFeature extrude, out Solid solid, out string failureMessage)
        => TryCreateExtrudedSolid(sketch, extrude, out solid, out failureMessage);

    private static bool TryCreateExtrudedSolid(
        SketchFeature sketch,
        ExtrudeFeature extrude,
        out Solid solid,
        out string failureMessage)
    {
        solid = new BoxSolid(1d, 1d, 1d);
        var depth = Math.Max(Math.Abs(extrude.Depth), 0.2d);

        if (!ProfileBuilder.TryBuild(sketch, out var profile, out failureMessage))
            return false;

        var extrudeSolid = new ExtrudeSolid(profile, depth)
        {
            Name = sketch.Name,
            Plane = ProfileBuilder.GetOrientation(sketch.PlaneName),
            Symmetric = extrude.Symmetric,
            TaperAngleDegrees = extrude.TaperAngleDegrees,
            FacePlane = sketch.FacePlane?.ToFacePlaneData()
        };

        if (extrude.ReverseDirection && !extrude.Symmetric)
        {
            if (extrudeSolid.FacePlane is { } facePlaneData)
            {
                var reversedFp = facePlaneData with
                {
                    NX = -facePlaneData.NX,
                    NY = -facePlaneData.NY,
                    NZ = -facePlaneData.NZ
                };
                solid = new ExtrudeSolid(profile, depth)
                {
                    Name = sketch.Name,
                    Plane = extrudeSolid.Plane,
                    Symmetric = extrude.Symmetric,
                    TaperAngleDegrees = extrude.TaperAngleDegrees,
                    FacePlane = reversedFp
                };
            }
            else
            {
                var translation = extrudeSolid.Plane switch
                {
                    PlaneOrientation.Front => Transform3D.CreateTranslation(-depth, 0d, 0d),
                    PlaneOrientation.Right => Transform3D.CreateTranslation(0d, -depth, 0d),
                    _ => Transform3D.CreateTranslation(0d, 0d, -depth)
                };

                solid = new TransformedSolid(extrudeSolid, translation)
                {
                    Name = sketch.Name
                };
            }
        }
        else
        {
            solid = extrudeSolid;
        }

        failureMessage = string.Empty;
        return true;
    }

    private static string ResolveActivePlaneName(CadProject project)
    {
        if (project.ActiveSketchSession is not null)
        {
            return project.ActiveSketchSession.PlaneName;
        }

        if (project.Selection.Kind == CadEntityKind.ReferencePlane)
        {
            var selectedPlane = project.Scene.ReferencePlanes.FirstOrDefault(plane => plane.Id == project.Selection.EntityId);
            if (selectedPlane is not null)
            {
                return selectedPlane.Name;
            }
        }

        return project.Scene.ReferencePlanes.FirstOrDefault(plane => plane.Kind == CadReferencePlaneKind.Top)?.Name ?? "Top";
    }
}

public static class CadProjectFactory
{
    public static CadProject CreateDefault()
    {
        var project = new CadProject();
        project.Scene.EnsureReferencePlanes();

        var topPlane = project.Scene.ReferencePlanes.FirstOrDefault(plane => plane.Kind == CadReferencePlaneKind.Top);
        if (topPlane is not null)
        {
            project.Selection = new CadSelection(CadEntityKind.ReferencePlane, topPlane.Id, topPlane.Name);
        }

        return project;
    }
}

public static class CadProjectTextExporter
{
    public static string Export(CadProject project)
    {
        var lines = new List<string>
        {
            $"project {Sanitize(project.Name)}",
            $"units {project.Units}",
            $"mode {project.ActiveMode}",
            string.Empty,
            $"scene {Sanitize(project.Scene.Name)} {{"
        };

        foreach (var plane in project.Scene.ReferencePlanes)
        {
            lines.Add($"  plane {Sanitize(plane.Name)} ({plane.Kind})");
        }

        foreach (var body in project.Scene.Bodies)
        {
            lines.Add($"  body {Sanitize(body.Name)} {{");

            foreach (var feature in body.Features)
            {
                foreach (var featureLine in DescribeFeatureLines(feature))
                {
                    lines.Add($"    {featureLine}");
                }
            }

            lines.Add("  }");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> DescribeFeatureLines(CadFeature feature)
    {
        return feature switch
        {
            BoxFeature box => [$"create {Sanitize(feature.Name)}: box(width={Format(box.Width)}, depth={Format(box.Depth)}, height={Format(box.Height)})"],
            CylinderFeature cylinder => [$"create {Sanitize(feature.Name)}: cylinder(radius={Format(cylinder.Radius)}, height={Format(cylinder.Height)})"],
            SphereFeature sphere => [$"create {Sanitize(feature.Name)}: sphere(radius={Format(sphere.Radius)})"],
            ConeFeature cone => [$"create {Sanitize(feature.Name)}: cone(radius={Format(cone.Radius)}, height={Format(cone.Height)})"],
            TorusFeature torus => [$"create {Sanitize(feature.Name)}: torus(major={Format(torus.MajorRadius)}, minor={Format(torus.MinorRadius)})"],
            PyramidFeature pyramid => [$"create {Sanitize(feature.Name)}: pyramid(width={Format(pyramid.BaseWidth)}, depth={Format(pyramid.BaseDepth)}, height={Format(pyramid.Height)})"],
            WedgeFeature wedge => [$"create {Sanitize(feature.Name)}: wedge(width={Format(wedge.Width)}, depth={Format(wedge.Depth)}, height={Format(wedge.Height)})"],
            EllipsoidFeature ellipsoid => [$"create {Sanitize(feature.Name)}: ellipsoid(rx={Format(ellipsoid.RadiusX)}, ry={Format(ellipsoid.RadiusY)}, rz={Format(ellipsoid.RadiusZ)})"],
            CapsuleFeature capsule => [$"create {Sanitize(feature.Name)}: capsule(radius={Format(capsule.Radius)}, height={Format(capsule.Height)})"],
            HemisphereFeature hemisphere => [$"create {Sanitize(feature.Name)}: hemisphere(radius={Format(hemisphere.Radius)})"],
            PrismFeature prism => [$"create {Sanitize(feature.Name)}: prism(radius={Format(prism.Radius)}, height={Format(prism.Height)}, sides={prism.Sides.ToString(CultureInfo.InvariantCulture)})"],
            ArrowFeature arrow => [$"create {Sanitize(feature.Name)}: arrow(shaftRadius={Format(arrow.ShaftRadius)}, shaftHeight={Format(arrow.ShaftHeight)}, headRadius={Format(arrow.HeadRadius)}, headHeight={Format(arrow.HeadHeight)})"],
            IcosphereFeature icosphere => [$"create {Sanitize(feature.Name)}: icosphere(radius={Format(icosphere.Radius)}, subdivisions={icosphere.Subdivisions.ToString(CultureInfo.InvariantCulture)})"],
            TetrahedronFeature tetrahedron => [$"create {Sanitize(feature.Name)}: tetrahedron(radius={Format(tetrahedron.Radius)})"],
            OctahedronFeature octahedron => [$"create {Sanitize(feature.Name)}: octahedron(radius={Format(octahedron.Radius)})"],
            IcosahedronFeature icosahedron => [$"create {Sanitize(feature.Name)}: icosahedron(radius={Format(icosahedron.Radius)})"],
            MoveFeature move => [$"op {Sanitize(feature.Name)}: move(x={Format(move.X)}, y={Format(move.Y)}, z={Format(move.Z)})"],
            SketchFeature sketch => DescribeSketchFeatureLines(sketch),
            ExtrudeFeature extrude => [$"feature {Sanitize(feature.Name)}: extrude(sketch={Sanitize(extrude.SketchName)}, depth={Format(extrude.Depth)}, direction={(extrude.ReverseDirection ? "reverse" : "normal")}, operation={(extrude.Symmetric ? CadExtrudeOperation.Symmetric : extrude.Operation)})"],
            RevolveFeature revolve => [$"feature {Sanitize(feature.Name)}: revolve(sketch={Sanitize(revolve.SketchName)}, angle={Format(revolve.AngleDegrees)}, axis={revolve.Axis})"],
            FilletFeature fillet => [$"op {Sanitize(feature.Name)}: fillet(radius={Format(fillet.Radius)})"],
            ChamferFeature chamfer => [$"op {Sanitize(feature.Name)}: chamfer(distance={Format(chamfer.Distance)})"],
            ShellFeature shell => [$"op {Sanitize(feature.Name)}: shell(thickness={Format(shell.Thickness)})"],
            MirrorFeature mirror => [$"op {Sanitize(feature.Name)}: mirror(axis={mirror.Axis})"],
            LinearPatternFeature lp => [$"op {Sanitize(feature.Name)}: linearpattern(count={lp.Count.ToString(CultureInfo.InvariantCulture)}, spacing={Format(lp.Spacing)}, axis={lp.Axis})"],
            CircularPatternFeature cp => [$"op {Sanitize(feature.Name)}: circularpattern(count={cp.Count.ToString(CultureInfo.InvariantCulture)}, angle={Format(cp.TotalAngle)}, axis={cp.Axis})"],
            // region: task 29 start
            HoleFeature hole => [$"op {Sanitize(feature.Name)}: hole(diameter={Format(hole.Diameter)}, depth={hole.DepthLabel}, offsetX={Format(hole.CenterOffsetX)}, offsetY={Format(hole.CenterOffsetY)})"],
            // region: task 29 end
            PlaceholderFeature placeholder => [$"future {Sanitize(feature.Name)}: {Sanitize(placeholder.PlaceholderKind)}"],
            _ => [$"feature {Sanitize(feature.Name)}: {feature.Kind}"]
        };
    }

    private static IReadOnlyList<string> DescribeSketchFeatureLines(SketchFeature sketch)
    {
        var lines = new List<string>
        {
            $"sketch {Sanitize(sketch.Name)} on {Sanitize(sketch.PlaneName)} {{"
        };

        var normalizedEntities = ExpandSketchGeometry(sketch.Entities).ToList();
        for (var index = 0; index < normalizedEntities.Count; index++)
        {
            lines.Add($"  {DescribeSketchEntity(normalizedEntities[index], index + 1)}");
        }

        lines.Add($"  profile(closed={(ProfileBuilder.CanBuildClosedProfile(normalizedEntities) ? "yes" : "no")})");
        lines.Add("}");
        return lines;
    }

    private static IEnumerable<CadSketchEntity> ExpandSketchGeometry(IReadOnlyList<CadSketchEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity is not CadSketchRectangle rectangle)
            {
                yield return entity;
                continue;
            }

            var minX = rectangle.X;
            var minY = rectangle.Y;
            var maxX = rectangle.X + rectangle.Width;
            var maxY = rectangle.Y + rectangle.Height;

            yield return CreateSketchLine(minX, minY, maxX, minY, rectangle);
            yield return CreateSketchLine(maxX, minY, maxX, maxY, rectangle);
            yield return CreateSketchLine(maxX, maxY, minX, maxY, rectangle);
            yield return CreateSketchLine(minX, maxY, minX, minY, rectangle);
        }
    }

    private static CadSketchLine CreateSketchLine(
        double startX,
        double startY,
        double endX,
        double endY,
        CadSketchEntity source)
    {
        return new CadSketchLine
        {
            StartX = startX,
            StartY = startY,
            EndX = endX,
            EndY = endY,
            IsConstruction = source.IsConstruction,
            IsFixed = source.IsFixed
        };
    }

    private static string DescribeSketchEntity(CadSketchEntity entity, int index)
    {
        var qualifier = SketchEntityQualifier(entity);
        return entity switch
        {
            CadSketchPoint point =>
                $"{qualifier}point P{index}(x={Format(point.X)}, y={Format(point.Y)})",
            CadSketchLine line =>
                $"{qualifier}line L{index}(start=({Format(line.StartX)}, {Format(line.StartY)}), end=({Format(line.EndX)}, {Format(line.EndY)}))",
            CadSketchCircle circle =>
                $"{qualifier}circle C{index}(center=({Format(circle.CenterX)}, {Format(circle.CenterY)}), radius={Format(circle.Radius)})",
            CadSketchArc arc =>
                $"{qualifier}arc A{index}(center=({Format(arc.CenterX)}, {Format(arc.CenterY)}), radius={Format(arc.Radius)}, startAngle={Format(arc.StartAngleDegrees)}, endAngle={Format(arc.EndAngleDegrees)})",
            CadSketchRectangle rectangle =>
                $"{qualifier}rectangle R{index}(x={Format(rectangle.X)}, y={Format(rectangle.Y)}, width={Format(rectangle.Width)}, height={Format(rectangle.Height)})",
            CadSketchPolygon polygon =>
                $"{qualifier}polygon G{index}(center=({Format(polygon.CenterX)}, {Format(polygon.CenterY)}), radius={Format(polygon.Radius)}, sides={polygon.Sides})",
            CadSketchSlot slot =>
                $"{qualifier}slot S{index}(c1=({Format(slot.Center1X)}, {Format(slot.Center1Y)}), c2=({Format(slot.Center2X)}, {Format(slot.Center2Y)}), radius={Format(slot.Radius)})",
            CadSketchSpline spline =>
                $"{qualifier}spline SP{index}(controlPoints={spline.ControlPointsXY.Count / 2})",
            _ => $"{qualifier}{Sanitize(entity.EntityType)}_{index}"
        };
    }

    private static string SketchEntityQualifier(CadSketchEntity entity)
    {
        return (entity.IsConstruction, entity.IsFixed) switch
        {
            (true, true) => "construction fixed ",
            (true, false) => "construction ",
            (false, true) => "fixed ",
            _ => string.Empty
        };
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "entity";
        }

        var result = new System.Text.StringBuilder();
        foreach (var c in text.Trim())
        {
            result.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
        }

        return result.ToString().Trim('_');
    }

}
