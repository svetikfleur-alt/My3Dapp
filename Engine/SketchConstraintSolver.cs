using System.Text.Json;
using FormaCore.Core;

namespace FormaCore.Engine;

public sealed class SolverInput
{
    public IReadOnlyList<CadSketchEntity> Entities { get; init; } = [];

    public IReadOnlyList<CadSketchConstraint> Constraints { get; init; } = [];

    public IReadOnlyList<CadSketchDimension> Dimensions { get; init; } = [];

    public string Units { get; init; } = "mm";

    public int Iterations { get; init; } = 4;
}

public sealed class SolverResult
{
    public CadSketchSolveStatus Status { get; init; } = CadSketchSolveStatus.Underdefined;

    public IReadOnlyList<CadSketchEntity> UpdatedEntities { get; init; } = [];

    public IReadOnlyList<CadSketchConstraint> Constraints { get; init; } = [];

    public IReadOnlyList<CadSketchDimension> Dimensions { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int? DegreesOfFreedomEstimate { get; init; }
}

public interface ISketchConstraintSolver
{
    SolverResult Solve(SolverInput input);
}

public sealed class SimpleSketchConstraintSolver : ISketchConstraintSolver
{
    private const double MinimumLength = 0.001d;
    private const double MinimumRadius = 0.2d;

    public SolverResult Solve(SolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var entities = CloneEntities(input.Entities);
        var constraints = CloneConstraints(input.Constraints);
        var dimensions = CloneDimensions(input.Dimensions);
        var errors = new List<string>();
        var warnings = new List<string>();
        var entityById = entities.ToDictionary(entity => entity.Id);

        if (entities.Count == 0)
        {
            return new SolverResult
            {
                Status = CadSketchSolveStatus.Underdefined,
                UpdatedEntities = entities,
                Constraints = constraints,
                Dimensions = dimensions,
                Warnings = ["Sketch contains no entities."]
            };
        }

        for (var iteration = 0; iteration < Math.Max(1, input.Iterations); iteration++)
        {
            foreach (var constraint in constraints)
            {
                ApplyConstraint(constraint, entityById, constraints, errors, warnings);
            }

            foreach (var dimension in dimensions)
            {
                ApplyDimension(dimension, entityById, constraints, errors, warnings);
            }
        }

        var dof = ComputeDegreesOfFreedomEstimate(entities, constraints, dimensions);
        var hasFailures = constraints.Any(item => item.Status == CadSketchConstraintStatus.Failed)
            || dimensions.Any(item => item.Status == CadSketchDimensionStatus.Failed);
        var hasUnsupported = constraints.Any(item => item.Status == CadSketchConstraintStatus.Unsupported)
            || dimensions.Any(item => item.Status == CadSketchDimensionStatus.Unsupported);

        var status = hasFailures
            ? CadSketchSolveStatus.Failed
            : hasUnsupported
                ? CadSketchSolveStatus.Unsupported
                : dof <= 0
                    ? CadSketchSolveStatus.Solved
                    : CadSketchSolveStatus.Underdefined;

        return new SolverResult
        {
            Status = status,
            UpdatedEntities = entities,
            Constraints = constraints,
            Dimensions = dimensions,
            Errors = errors,
            Warnings = warnings,
            DegreesOfFreedomEstimate = dof
        };
    }

    private static void ApplyConstraint(
        CadSketchConstraint constraint,
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> allConstraints,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        var entities = ResolveEntities(constraint, entityById, errors);
        if (entities is null)
        {
            constraint.Status = CadSketchConstraintStatus.Failed;
            return;
        }

        switch (constraint.Kind)
        {
            case CadSketchConstraintKind.Horizontal:
                if (entities.Count == 1 && entities[0] is CadSketchLine horizontalLine)
                {
                    ApplyHorizontalConstraint(entityById, allConstraints, horizontalLine);
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, $"Horizontal constraint requires one line entity.", errors);
                }
                break;

            case CadSketchConstraintKind.Vertical:
                if (entities.Count == 1 && entities[0] is CadSketchLine verticalLine)
                {
                    ApplyVerticalConstraint(entityById, allConstraints, verticalLine);
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, $"Vertical constraint requires one line entity.", errors);
                }
                break;

            case CadSketchConstraintKind.Coincident:
                if (entities.Count >= 2)
                {
                    if (ApplyCoincidentToEntities(entities))
                    {
                        constraint.Status = CadSketchConstraintStatus.Active;
                        constraint.Error = null;
                    }
                    else
                    {
                        FailConstraint(constraint, "Coincident constraint could not be applied to fixed or unsupported entities.", errors);
                    }
                }
                else
                {
                    FailConstraint(constraint, "Coincident constraint requires two referenced entities.", errors);
                }
                break;

            case CadSketchConstraintKind.Fixed:
                if (entities.Count >= 1)
                {
                    foreach (var entity in entities)
                    {
                        entity.IsFixed = true;
                    }

                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, "Fixed constraint requires at least one entity.", errors);
                }
                break;

            case CadSketchConstraintKind.EqualLength:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine referenceLine &&
                    entities[1] is CadSketchLine drivenLine)
                {
                    if (drivenLine.IsFixed)
                    {
                        FailConstraint(constraint, "Equal length cannot drive a fixed line.", errors);
                        break;
                    }

                    SetLineLengthWithAnchor(entityById, allConstraints, drivenLine, LineLength(referenceLine));
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, "Equal length currently supports two line entities.", errors);
                }
                break;

            case CadSketchConstraintKind.Parallel:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine refParallel &&
                    entities[1] is CadSketchLine drivenParallel)
                {
                    if (drivenParallel.IsFixed)
                    {
                        FailConstraint(constraint, "Parallel cannot drive a fixed line.", errors);
                        break;
                    }

                    ApplyParallelConstraint(entityById, allConstraints, refParallel, drivenParallel);
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, "Parallel requires two line entities.", errors);
                }
                break;

            case CadSketchConstraintKind.Perpendicular:
                if (entities.Count >= 2 &&
                    entities[0] is CadSketchLine refPerp &&
                    entities[1] is CadSketchLine drivenPerp)
                {
                    if (drivenPerp.IsFixed)
                    {
                        FailConstraint(constraint, "Perpendicular cannot drive a fixed line.", errors);
                        break;
                    }

                    ApplyPerpendicularConstraint(entityById, allConstraints, refPerp, drivenPerp);
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, "Perpendicular requires two line entities.", errors);
                }
                break;

            case CadSketchConstraintKind.EqualRadius:
                if (entities.Count >= 2 &&
                    TryGetRadius(entities[0], out var refRadius) &&
                    TrySetRadius(entities[1], refRadius))
                {
                    constraint.Status = CadSketchConstraintStatus.Active;
                    constraint.Error = null;
                }
                else
                {
                    FailConstraint(constraint, "Equal radius requires non-fixed circle or arc entities.", errors);
                }
                break;

            default:
                constraint.Status = CadSketchConstraintStatus.Unsupported;
                constraint.Error = $"Constraint type '{constraint.Kind}' is not supported by Constraint Solver Lite.";
                warnings.Add(constraint.Error);
                break;
        }
    }

    private static void ApplyDimension(
        CadSketchDimension dimension,
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> allConstraints,
        ICollection<string> errors,
        ICollection<string> warnings)
    {
        var entities = ResolveEntities(dimension, entityById, errors);
        if (entities is null)
        {
            dimension.Status = CadSketchDimensionStatus.Failed;
            return;
        }

        if (dimension.IsDriven)
        {
            dimension.Status = CadSketchDimensionStatus.Active;
            dimension.Error = null;
            return;
        }

        switch (dimension.Kind)
        {
            case CadSketchDimensionKind.Length:
                if (entities.Count == 1 && entities[0] is CadSketchLine line)
                {
                    if (line.IsFixed)
                    {
                        FailDimension(dimension, "Length dimension cannot drive a fixed line.", errors);
                        break;
                    }

                    SetLineLengthWithAnchor(entityById, allConstraints, line, dimension.Value);
                    dimension.Status = CadSketchDimensionStatus.Active;
                    dimension.Error = null;
                }
                else if (entities.Count == 1 && entities[0] is CadSketchRectangle rectangle && !string.IsNullOrWhiteSpace(dimension.ParameterKey))
                {
                    if (string.Equals(dimension.ParameterKey, "Width", StringComparison.OrdinalIgnoreCase))
                    {
                        rectangle.Width = Math.Max(Math.Abs(dimension.Value), MinimumRadius);
                        dimension.Status = CadSketchDimensionStatus.Active;
                        dimension.Error = null;
                    }
                    else if (string.Equals(dimension.ParameterKey, "Height", StringComparison.OrdinalIgnoreCase))
                    {
                        rectangle.Height = Math.Max(Math.Abs(dimension.Value), MinimumRadius);
                        dimension.Status = CadSketchDimensionStatus.Active;
                        dimension.Error = null;
                    }
                    else
                    {
                        dimension.Status = CadSketchDimensionStatus.Unsupported;
                        dimension.Error = $"Rectangle length dimension '{dimension.ParameterKey}' is not supported.";
                        warnings.Add(dimension.Error);
                    }
                }
                else if (entities.Count == 2)
                {
                    ApplyPointDistanceDimension(entities[0], entities[1], dimension.Value, dimension.ParameterKey);
                    dimension.Status = CadSketchDimensionStatus.Active;
                    dimension.Error = null;
                }
                else
                {
                    dimension.Status = CadSketchDimensionStatus.Unsupported;
                    dimension.Error = "Length dimensions support lines, rectangle width/height, and point-to-point distances.";
                    warnings.Add(dimension.Error);
                }
                break;

            case CadSketchDimensionKind.Radius:
                if (entities.Count == 1 && TrySetRadius(entities[0], dimension.Value))
                {
                    dimension.Status = CadSketchDimensionStatus.Active;
                    dimension.Error = null;
                }
                else
                {
                    FailDimension(dimension, "Radius dimension requires a non-fixed circle or arc.", errors);
                }
                break;

            case CadSketchDimensionKind.Diameter:
                if (entities.Count == 1 && TrySetRadius(entities[0], dimension.Value / 2d))
                {
                    dimension.Status = CadSketchDimensionStatus.Active;
                    dimension.Error = null;
                }
                else
                {
                    FailDimension(dimension, "Diameter dimension requires a non-fixed circle or arc.", errors);
                }
                break;

            case CadSketchDimensionKind.Angle:
                if (entities.Count == 2 &&
                    entities[0] is CadSketchLine lineA &&
                    entities[1] is CadSketchLine lineB)
                {
                    if (lineB.IsFixed && !lineA.IsFixed)
                    {
                        ApplyAngleDimension(entityById, allConstraints, lineB, lineA, dimension.Value);
                        dimension.Status = CadSketchDimensionStatus.Active;
                        dimension.Error = null;
                    }
                    else if (!lineB.IsFixed)
                    {
                        ApplyAngleDimension(entityById, allConstraints, lineA, lineB, dimension.Value);
                        dimension.Status = CadSketchDimensionStatus.Active;
                        dimension.Error = null;
                    }
                    else
                    {
                        FailDimension(dimension, "Angle dimension cannot drive two fixed lines.", errors);
                    }
                }
                else
                {
                    FailDimension(dimension, "Angle dimension requires two lines.", errors);
                }
                break;

            default:
                dimension.Status = CadSketchDimensionStatus.Unsupported;
                dimension.Error = $"Dimension type '{dimension.Kind}' is not supported by Constraint Solver Lite.";
                warnings.Add(dimension.Error);
                break;
        }
    }

    private static List<CadSketchEntity>? ResolveEntities(
        CadSketchConstraint constraint,
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        ICollection<string> errors)
    {
        var entities = new List<CadSketchEntity>();
        foreach (var entityId in constraint.EntityIds)
        {
            if (!entityById.TryGetValue(entityId, out var entity))
            {
                var error = $"Constraint '{constraint.Kind}' references unknown entity '{entityId}'.";
                constraint.Error = error;
                errors.Add(error);
                return null;
            }

            entities.Add(entity);
        }

        return entities;
    }

    private static List<CadSketchEntity>? ResolveEntities(
        CadSketchDimension dimension,
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        ICollection<string> errors)
    {
        var entities = new List<CadSketchEntity>();
        foreach (var entityId in dimension.EntityIds)
        {
            if (!entityById.TryGetValue(entityId, out var entity))
            {
                var error = $"Dimension '{dimension.Label}' references unknown entity '{entityId}'.";
                dimension.Error = error;
                errors.Add(error);
                return null;
            }

            entities.Add(entity);
        }

        return entities;
    }

    private static List<CadSketchEntity> CloneEntities(IReadOnlyList<CadSketchEntity> entities)
    {
        if (entities.Count == 0)
        {
            return [];
        }

        var json = JsonSerializer.Serialize(entities);
        return JsonSerializer.Deserialize<List<CadSketchEntity>>(json) ?? [];
    }

    private static List<CadSketchConstraint> CloneConstraints(IReadOnlyList<CadSketchConstraint> constraints)
    {
        if (constraints.Count == 0)
        {
            return [];
        }

        return constraints.Select(CloneConstraint).ToList();
    }

    private static List<CadSketchDimension> CloneDimensions(IReadOnlyList<CadSketchDimension> dimensions)
    {
        if (dimensions.Count == 0)
        {
            return [];
        }

        return dimensions.Select(CloneDimension).ToList();
    }

    private static CadSketchConstraint CloneConstraint(CadSketchConstraint constraint)
    {
        return new CadSketchConstraint(constraint.Kind, constraint.Description, constraint.EntityIds, constraint.Parameters, constraint.Status, constraint.Error)
        {
            Id = constraint.Id
        };
    }

    private static CadSketchDimension CloneDimension(CadSketchDimension dimension)
    {
        return new CadSketchDimension(dimension.Kind, dimension.Label, dimension.Value, dimension.EntityIds, dimension.ParameterKey, dimension.IsDriven, dimension.Units, dimension.Status, dimension.Error)
        {
            Id = dimension.Id
        };
    }

    private static void FailConstraint(CadSketchConstraint constraint, string error, ICollection<string> errors)
    {
        constraint.Status = CadSketchConstraintStatus.Failed;
        constraint.Error = error;
        errors.Add(error);
    }

    private static void FailDimension(CadSketchDimension dimension, string error, ICollection<string> errors)
    {
        dimension.Status = CadSketchDimensionStatus.Failed;
        dimension.Error = error;
        errors.Add(error);
    }

    private static bool ApplyCoincidentToEntities(IReadOnlyList<CadSketchEntity> entities)
    {
        var reference = entities[0];
        var driven = entities[1];
        if (driven.IsFixed)
        {
            return false;
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
            return true;
        }

        if (reference is CadSketchPoint referencePoint && driven is CadSketchLine drivenLineFromPoint)
        {
            SnapLineEndpointToTarget(drivenLineFromPoint, new Vector2D(referencePoint.X, referencePoint.Y));
            return true;
        }

        if (reference is CadSketchLine referenceLineToPoint && driven is CadSketchPoint drivenPoint)
        {
            var endpoints = GetLineEndpoints(referenceLineToPoint);
            var startDistance = Distance(new Vector2D(drivenPoint.X, drivenPoint.Y), endpoints.Start);
            var endDistance = Distance(new Vector2D(drivenPoint.X, drivenPoint.Y), endpoints.End);
            var target = startDistance <= endDistance ? endpoints.Start : endpoints.End;
            drivenPoint.X = target.X;
            drivenPoint.Y = target.Y;
            return true;
        }

        if (reference is CadSketchPoint sourcePoint && driven is CadSketchPoint targetPoint)
        {
            targetPoint.X = sourcePoint.X;
            targetPoint.Y = sourcePoint.Y;
            return true;
        }

        if (TryGetCenter(reference, out var center))
        {
            if (driven is CadSketchPoint point)
            {
                point.X = center.X;
                point.Y = center.Y;
                return true;
            }

            return TrySetCenter(driven, center.X, center.Y);
        }

        return false;
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

        var safeLength = Math.Max(Math.Abs(targetLength), MinimumLength);
        var ux = (line.EndX - line.StartX) / currentLength;
        var uy = (line.EndY - line.StartY) / currentLength;
        line.EndX = line.StartX + ux * safeLength;
        line.EndY = line.StartY + uy * safeLength;
    }

    private static void SetLineLengthWithAnchor(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine line,
        double targetLength)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, line);
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

    private static void ApplyHorizontalConstraint(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine line)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, line);
        if (anchorIndex == 0)
        {
            line.EndY = line.StartY;
        }
        else
        {
            line.StartY = line.EndY;
        }
    }

    private static void ApplyVerticalConstraint(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine line)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, line);
        if (anchorIndex == 0)
        {
            line.EndX = line.StartX;
        }
        else
        {
            line.StartX = line.EndX;
        }
    }

    private static bool TryGetRadius(CadSketchEntity entity, out double radius)
    {
        switch (entity)
        {
            case CadSketchCircle circle:
                radius = circle.Radius;
                return true;
            case CadSketchArc arc:
                radius = arc.Radius;
                return true;
            default:
                radius = 0d;
                return false;
        }
    }

    private static void ApplyParallelConstraint(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine refLine,
        CadSketchLine drivenLine)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, drivenLine);
        var refDx = refLine.EndX - refLine.StartX;
        var refDy = refLine.EndY - refLine.StartY;
        var len = LineLength(drivenLine);
        var refLen = LineLength(refLine);
        if (refLen < 1e-9d) return;

        var ux = refDx / refLen;
        var uy = refDy / refLen;
        
        var drivenDx = drivenLine.EndX - drivenLine.StartX;
        var drivenDy = drivenLine.EndY - drivenLine.StartY;
        if (ux * drivenDx + uy * drivenDy < 0) {
            ux = -ux;
            uy = -uy;
        }

        if (anchorIndex == 0)
        {
            drivenLine.EndX = drivenLine.StartX + ux * len;
            drivenLine.EndY = drivenLine.StartY + uy * len;
        }
        else
        {
            drivenLine.StartX = drivenLine.EndX - ux * len;
            drivenLine.StartY = drivenLine.EndY - uy * len;
        }
    }

    private static void ApplyPerpendicularConstraint(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine refLine,
        CadSketchLine drivenLine)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, drivenLine);
        var refDx = refLine.EndX - refLine.StartX;
        var refDy = refLine.EndY - refLine.StartY;
        var len = LineLength(drivenLine);
        var refLen = LineLength(refLine);
        if (refLen < 1e-9d) return;

        var ux = -refDy / refLen;
        var uy = refDx / refLen;

        var drivenDx = drivenLine.EndX - drivenLine.StartX;
        var drivenDy = drivenLine.EndY - drivenLine.StartY;
        if (ux * drivenDx + uy * drivenDy < 0) {
            ux = -ux;
            uy = -uy;
        }

        if (anchorIndex == 0)
        {
            drivenLine.EndX = drivenLine.StartX + ux * len;
            drivenLine.EndY = drivenLine.StartY + uy * len;
        }
        else
        {
            drivenLine.StartX = drivenLine.EndX - ux * len;
            drivenLine.StartY = drivenLine.EndY - uy * len;
        }
    }

    private static void ApplyAngleDimension(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine lineA,
        CadSketchLine lineB,
        double targetAngleDegrees)
    {
        var anchorIndex = GetPreferredLineAnchorIndex(entityById, constraints, lineB);
        var dxA = lineA.EndX - lineA.StartX;
        var dyA = lineA.EndY - lineA.StartY;
        var baseAngle = Math.Atan2(dyA, dxA);
        
        var targetAngleRad = targetAngleDegrees * Math.PI / 180.0;
        var newAngle = baseAngle + targetAngleRad;
        
        var lenB = LineLength(lineB);
        if (lenB < 1e-9d) return;
        
        var ux = Math.Cos(newAngle);
        var uy = Math.Sin(newAngle);

        if (anchorIndex == 0)
        {
            lineB.EndX = lineB.StartX + ux * lenB;
            lineB.EndY = lineB.StartY + uy * lenB;
        }
        else
        {
            lineB.StartX = lineB.EndX - ux * lenB;
            lineB.StartY = lineB.EndY - uy * lenB;
        }
    }

    private static void ApplyPointDistanceDimension(
        CadSketchEntity entityA,
        CadSketchEntity entityB,
        double targetDist,
        string? parameterKey)
    {
        if (!TryGetCenter(entityA, out var centerA) || !TryGetCenter(entityB, out var centerB)) return;
        
        var isAFixed = entityA.IsFixed;
        var isBFixed = entityB.IsFixed;
        if (isAFixed && isBFixed) return;

        var dx = centerB.X - centerA.X;
        var dy = centerB.Y - centerA.Y;
        var currentDist = Math.Sqrt(dx * dx + dy * dy);
        
        if (currentDist < 1e-9d) {
            dx = 1; dy = 0; currentDist = 1;
        }
        
        var targetX = centerB.X;
        var targetY = centerB.Y;
        
        if (string.Equals(parameterKey, "Horizontal", StringComparison.OrdinalIgnoreCase))
        {
            var sign = dx >= 0 ? 1 : -1;
            targetX = centerA.X + sign * targetDist;
        }
        else if (string.Equals(parameterKey, "Vertical", StringComparison.OrdinalIgnoreCase))
        {
            var sign = dy >= 0 ? 1 : -1;
            targetY = centerA.Y + sign * targetDist;
        }
        else
        {
            var ux = dx / currentDist;
            var uy = dy / currentDist;
            targetX = centerA.X + ux * targetDist;
            targetY = centerA.Y + uy * targetDist;
        }

        if (isAFixed)
        {
            TrySetCenter(entityB, targetX, targetY);
        }
        else if (isBFixed)
        {
            // Move A instead
            if (string.Equals(parameterKey, "Horizontal", StringComparison.OrdinalIgnoreCase)) {
                TrySetCenter(entityA, centerB.X - (dx >= 0 ? 1 : -1) * targetDist, centerA.Y);
            } else if (string.Equals(parameterKey, "Vertical", StringComparison.OrdinalIgnoreCase)) {
                TrySetCenter(entityA, centerA.X, centerB.Y - (dy >= 0 ? 1 : -1) * targetDist);
            } else {
                var ux = dx / currentDist;
                var uy = dy / currentDist;
                TrySetCenter(entityA, centerB.X - ux * targetDist, centerB.Y - uy * targetDist);
            }
        }
        else
        {
            // Move B
            TrySetCenter(entityB, targetX, targetY);
        }
    }

    private static void SnapLineEndpointToTarget(CadSketchLine line, Vector2D target)
    {
        var endpoints = GetLineEndpoints(line);
        var startDistance = Distance(endpoints.Start, target);
        var endDistance = Distance(endpoints.End, target);
        SetLineEndpoint(line, startDistance <= endDistance ? 0 : 1, target);
    }

    private static int GetPreferredLineAnchorIndex(
        IReadOnlyDictionary<Guid, CadSketchEntity> entityById,
        IReadOnlyList<CadSketchConstraint> constraints,
        CadSketchLine line,
        Vector2D? preferredAnchor = null)
    {
        if (preferredAnchor is Vector2D anchor)
        {
            var endpoints = GetLineEndpoints(line);
            return Distance(endpoints.Start, anchor) <= Distance(endpoints.End, anchor) ? 0 : 1;
        }

        var endpointsForLine = GetLineEndpoints(line);
        foreach (var constraint in constraints.Where(item =>
                     item.Kind == CadSketchConstraintKind.Coincident &&
                     item.EntityIds.Contains(line.Id)))
        {
            foreach (var entityId in constraint.EntityIds)
            {
                if (entityId == line.Id)
                {
                    continue;
                }

                if (!entityById.TryGetValue(entityId, out var target) ||
                    !TryGetCenter(target, out var center))
                {
                    continue;
                }

                return Distance(endpointsForLine.Start, center) <= Distance(endpointsForLine.End, center) ? 0 : 1;
            }
        }

        return 0;
    }

    private static double LineLength(CadSketchLine line)
    {
        var dx = line.EndX - line.StartX;
        var dy = line.EndY - line.StartY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool TrySetRadius(CadSketchEntity entity, double radius)
    {
        var safeRadius = Math.Max(Math.Abs(radius), MinimumRadius);
        switch (entity)
        {
            case CadSketchCircle circle when !circle.IsFixed:
                circle.Radius = safeRadius;
                return true;
            case CadSketchArc arc when !arc.IsFixed:
                arc.Radius = safeRadius;
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

    private static int ComputeDegreesOfFreedomEstimate(
        IReadOnlyList<CadSketchEntity> entities,
        IReadOnlyList<CadSketchConstraint> constraints,
        IReadOnlyList<CadSketchDimension> dimensions)
    {
        var totalDof = entities.Sum(SketchEntityDof);
        var removedByConstraints = constraints
            .Where(item => item.Status == CadSketchConstraintStatus.Active)
            .Sum(item => SketchConstraintDofCost(item, entities));
        var removedByDimensions = dimensions.Count(item => !item.IsDriven && item.Status == CadSketchDimensionStatus.Active);
        return totalDof - removedByConstraints - removedByDimensions;
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
            CadSketchConstraintKind.Fixed => constraint.EntityIds
                .Select(id => entities.FirstOrDefault(entity => entity.Id == id))
                .Where(entity => entity is not null)
                .Sum(entity => SketchEntityDof(entity!)),
            CadSketchConstraintKind.Horizontal => 1,
            CadSketchConstraintKind.Vertical => 1,
            CadSketchConstraintKind.EqualLength => 1,
            _ => 0
        };
    }
}
