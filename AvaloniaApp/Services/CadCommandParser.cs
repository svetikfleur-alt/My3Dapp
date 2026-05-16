using System.Globalization;
using System.Text.RegularExpressions;

namespace My3DApp.AvaloniaApp.Services;

public enum CadViewportCommandKind
{
    AddPrimitive,
    MoveSelected,
    DeleteSelected,
    FocusCamera,
    SelectPlane,
    StartSketch,
    CancelSketch,
    SetSketchTool,
    UpdateSketchPreview,
    ClearSketchPreview,
    CancelSketchStep,
    PlaceSketchAt,
    FinishSketch,
    ApplySketchConstraint,
    ExtrudeSketch,
    RevolveSketch,
    SweepSketch,
    LoftProfiles,
    FilletBody,
    ChamferBody,
    ShellBody,
    MirrorBody,
    LinearPatternBody,
    CircularPatternBody,
    HoleBody,
    BooleanUnion,
    BooleanSubtract,
    BooleanIntersect
}

public sealed record CadViewportCommand(
    CadViewportCommandKind Kind,
    string? PrimitiveKind = null,
    string? SketchTool = null,
    string? ConstraintName = null,
    string? Plane = null,
    string? Axis = null,
    double Distance = 0d,
    double U = 0d,
    double V = 0d,
    Guid EntityIdA = default,
    Guid EntityIdB = default,
    string? ExtrudeOp = null,
    Guid TargetBodyId = default)
{
    public string Describe()
    {
        return Kind switch
        {
            CadViewportCommandKind.AddPrimitive when !string.IsNullOrWhiteSpace(PrimitiveKind) =>
                $"Add {PrimitiveKind}",
            CadViewportCommandKind.MoveSelected when !string.IsNullOrWhiteSpace(Axis) =>
                $"Move selected {Distance:0.###} in {Axis.ToUpperInvariant()}",
            CadViewportCommandKind.DeleteSelected => "Delete selected",
            CadViewportCommandKind.FocusCamera => "Focus camera",
            CadViewportCommandKind.SelectPlane when !string.IsNullOrWhiteSpace(Plane) =>
                $"Select plane {Plane}",
            CadViewportCommandKind.StartSketch when !string.IsNullOrWhiteSpace(Plane) =>
                $"Start sketch on {Plane}",
            CadViewportCommandKind.StartSketch => "Start sketch",
            CadViewportCommandKind.CancelSketch => "Cancel sketch",
            CadViewportCommandKind.SetSketchTool when !string.IsNullOrWhiteSpace(SketchTool) =>
                $"Use {SketchTool} sketch tool",
            CadViewportCommandKind.UpdateSketchPreview =>
                $"Preview sketch geometry at ({U:0.###}, {V:0.###})",
            CadViewportCommandKind.ClearSketchPreview =>
                "Clear sketch preview",
            CadViewportCommandKind.PlaceSketchAt =>
                $"Place sketch geometry at ({U:0.###}, {V:0.###})",
            CadViewportCommandKind.FinishSketch => "Finish sketch",
            CadViewportCommandKind.ApplySketchConstraint when !string.IsNullOrWhiteSpace(ConstraintName) =>
                $"Apply {ConstraintName} sketch constraint",
            CadViewportCommandKind.ExtrudeSketch when Distance > 0d =>
                $"Extrude selected sketch by {Distance:0.###}",
            CadViewportCommandKind.ExtrudeSketch when Distance < 0d =>
                $"Extrude selected sketch by {Math.Abs(Distance):0.###} reverse",
            CadViewportCommandKind.ExtrudeSketch => "Extrude selected sketch",
            CadViewportCommandKind.RevolveSketch when Distance > 0d && !string.IsNullOrWhiteSpace(Axis) =>
                $"Revolve selected sketch by {Distance:0.###} degrees around {Axis.ToUpperInvariant()}",
            CadViewportCommandKind.RevolveSketch when Distance > 0d =>
                $"Revolve selected sketch by {Distance:0.###} degrees",
            CadViewportCommandKind.RevolveSketch => "Revolve selected sketch",
            CadViewportCommandKind.SweepSketch when Distance > 0d =>
                $"Sweep selected sketch by {Distance:0.###}",
            CadViewportCommandKind.SweepSketch => "Sweep selected sketch",
            CadViewportCommandKind.LoftProfiles when Distance > 0d =>
                $"Loft profiles (distance={Distance:0.###})",
            CadViewportCommandKind.LoftProfiles => "Loft profiles",
            CadViewportCommandKind.FilletBody when Distance > 0d =>
                $"Fillet selected body (r={Distance:0.###})",
            CadViewportCommandKind.FilletBody => "Fillet selected body",
            CadViewportCommandKind.ChamferBody when Distance > 0d =>
                $"Chamfer selected body (d={Distance:0.###})",
            CadViewportCommandKind.ChamferBody => "Chamfer selected body",
            CadViewportCommandKind.ShellBody when Distance > 0d =>
                $"Shell selected body (t={Distance:0.###})",
            CadViewportCommandKind.ShellBody => "Shell selected body",
            CadViewportCommandKind.MirrorBody when !string.IsNullOrWhiteSpace(Axis) =>
                $"Mirror selected body across {Axis.ToUpperInvariant()}",
            CadViewportCommandKind.MirrorBody => "Mirror selected body",
            CadViewportCommandKind.LinearPatternBody when Distance > 0d =>
                $"Linear pattern: {(int)Distance}x, spacing={U:0.###} along {Axis?.ToUpperInvariant() ?? "X"}",
            CadViewportCommandKind.LinearPatternBody => "Linear pattern selected body",
            CadViewportCommandKind.CircularPatternBody when Distance > 0d =>
                $"Circular pattern: {(int)Distance}x, {U:0.###}° around {Axis?.ToUpperInvariant() ?? "Y"}",
            CadViewportCommandKind.CircularPatternBody => "Circular pattern selected body",
            _ => Kind.ToString()
        };
    }
}

public sealed class CadCommandParseResult
{
    private CadCommandParseResult(bool isSuccess, string message, IReadOnlyList<CadViewportCommand> commands)
    {
        IsSuccess = isSuccess;
        Message = message;
        Commands = commands;
    }

    public bool IsSuccess { get; }

    public string Message { get; }

    public CadViewportCommand? Command => Commands.Count > 0 ? Commands[0] : null;

    public IReadOnlyList<CadViewportCommand> Commands { get; }

    public static CadCommandParseResult Success(CadViewportCommand command, string message) =>
        new(true, message, [command]);

    public static CadCommandParseResult Success(IReadOnlyList<CadViewportCommand> commands, string message) =>
        commands.Count > 0
            ? new CadCommandParseResult(true, message, commands)
            : NotMatched("Command produced no executable steps.");

    public static CadCommandParseResult NotMatched(string message) =>
        new(false, message, []);
}

public sealed record CadCommandSequenceStep(
    int Index,
    string Text,
    CadCommandParseResult Result);

public sealed class CadCommandParser
{
    private const string NumberPattern = @"[+-]?\d+(?:\.\d+)?";
    private const string SketchToolNamePattern = @"line|rectangle|circle|arc|point|polygon|slot|spline|mirror|trim|offset|fillet2d|2d\s+fillet|fillet\s+2d|sketch\s+fillet|transform|angle\s+dimension|angle\s+dim|dimension";
    private const string ConstraintNamePattern = @"horizontal|vertical|coincident|equal|fix|fixed|tangent|parallel|perpendicular|concentric";

    private static readonly Regex CommandSeparatorPattern = new(
        @"(?:\r?\n|;|->|\bthen\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreatePrimitivePattern = new(
        @"^(?:create|add|make|insert|place)\s+(?:a\s+|an\s+|one\s+)?(?<primitive>box|cube|sphere|cylinder|cone|torus|pyramid|wedge|prism|capsule|hemisphere|ellipsoid|arrow|icosphere|tetrahedron|octahedron|icosahedron)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MovePattern = new(
        @"^move\s+(?:object|body|selected|selection)?\s*(?<distance>[+-]?\d+(?:\.\d+)?)\s*(?:in|on)?\s*(?<axis>[xyz])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeletePattern = new(
        @"^(?:delete|remove)\s*(?:object|body|selected|selection)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FocusPattern = new(
        @"^(?:focus|zoom)\s*(?:camera|selection|selected|object|here)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SelectPlanePattern = new(
        @"^(?:(?:select|choose|use)\s+)?(?<plane>top|front|right)(?:\s+plane)?$|^(?:select|choose|use)\s+plane\s+(?<plane2>top|front|right)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StartSketchPattern = new(
        @"^(?:start|begin|open|enter)\s+(?:a\s+)?sketch(?:\s+(?:on|at)\s+(?<plane>top|front|right)(?:\s+plane)?)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FinishSketchPattern = new(
        @"^(?:finish|end|close)\s+sketch$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CancelSketchPattern = new(
        @"^(?:cancel|abort)\s+sketch$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SketchToolPattern = new(
        $@"^(?:(?:sketch|draw|create|add|make)\s+)?(?:a\s+|an\s+)?(?<tool>{SketchToolNamePattern})$|^(?:use|set|choose|switch\s+to)\s+(?:sketch\s+)?(?<tool2>{SketchToolNamePattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConstraintPattern = new(
        $@"^(?:(?:apply|add|make|set)\s+)?(?<constraint>{ConstraintNamePattern})(?:\s+(?:constraint|sketch\s+constraint))?$|^(?:constraint)\s+(?<constraint2>{ConstraintNamePattern})$|^(?:make|set)\s+(?:last\s+)?(?:line|entity|curve)\s+(?<constraint3>horizontal|vertical|fixed)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PointCoordinatePattern = new(
        $@"^(?:(?:sketch|draw|create|add|place)\s+)?point(?:\s+at)?\s+(?<x>{NumberPattern})[,\s]+(?<y>{NumberPattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LineCoordinatePattern = new(
        $@"^(?:(?:sketch|draw|create|add|make)\s+)?line(?:\s+from)?\s+(?<x1>{NumberPattern})[,\s]+(?<y1>{NumberPattern})(?:\s+to)?\s+(?<x2>{NumberPattern})[,\s]+(?<y2>{NumberPattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RectangleCoordinatePattern = new(
        $@"^(?:(?:sketch|draw|create|add|make)\s+)?rectangle(?:\s+from)?\s+(?<x1>{NumberPattern})[,\s]+(?<y1>{NumberPattern})(?:\s+to)?\s+(?<x2>{NumberPattern})[,\s]+(?<y2>{NumberPattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CircleCoordinatePattern = new(
        $@"^(?:(?:sketch|draw|create|add|make)\s+)?circle(?:\s+(?:center|at))?\s+(?<cx>{NumberPattern})[,\s]+(?<cy>{NumberPattern})(?:\s+(?:radius|r))?\s+(?<r>{NumberPattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ArcCoordinatePattern = new(
        $@"^(?:(?:sketch|draw|create|add|make)\s+)?arc(?:\s+from)?\s+(?<x1>{NumberPattern})[,\s]+(?<y1>{NumberPattern})(?:\s+to)?\s+(?<x2>{NumberPattern})[,\s]+(?<y2>{NumberPattern})(?:\s+(?:through|via))?\s+(?<x3>{NumberPattern})[,\s]+(?<y3>{NumberPattern})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExtrudePattern = new(
        @"^extrude\b(?<tail>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RevolvePattern = new(
        @"^revolve(?:\s+(?<angle>[+-]?\d+(?:\.\d+)?))?(?:\s+(?:around|axis)\s+(?<axis>[xy]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FilletPattern = new(
        @"^fillet(?:\s+(?<radius>[+-]?\d+(?:\.\d+)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ShellPattern = new(
        @"^shell(?:\s+(?<thickness>[+-]?\d+(?:\.\d+)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MirrorPattern = new(
        @"^mirror(?:\s+(?<axis>[xyz]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChamferPattern = new(
        @"^chamfer(?:\s+(?<distance>[+-]?\d+(?:\.\d+)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LinearPatternPattern = new(
        @"^linear\s+pattern(?:\s+(?<count>\d+))?(?:\s+spacing\s+(?<spacing>[+-]?\d+(?:\.\d+)?))?(?:\s+(?:along|in)\s+(?<axis>[xyz]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CircularPatternPattern = new(
        @"^circular\s+pattern(?:\s+(?<count>\d+))?(?:\s+angle\s+(?<angle>[+-]?\d+(?:\.\d+)?))?(?:\s+(?:around|axis)\s+(?<axis>[xyz]))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HolePattern = new(
        @"^hole(?:\s+depth\s+(?<depth>[+-]?\d+(?:\.\d+)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BooleanPattern = new(
        @"^boolean\s+(?<op>union|join|subtract|cut|intersect)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<CadCommandSequenceStep> ParseSequence(string input)
    {
        var expanded = CadScriptLibrary.ExpandSequence(input);

        var parts = CommandSeparatorPattern
            .Split(expanded)
            .Select(NormalizeCommandText)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
        {
            return [new CadCommandSequenceStep(1, string.Empty, CadCommandParseResult.NotMatched("Command is empty."))];
        }

        var steps = new List<CadCommandSequenceStep>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            steps.Add(new CadCommandSequenceStep(i + 1, parts[i], Parse(parts[i])));
        }

        return steps;
    }

    public CadCommandParseResult Parse(string input)
    {
        var text = NormalizeCommandText(input);
        if (string.IsNullOrWhiteSpace(text))
        {
            return CadCommandParseResult.NotMatched("Command is empty.");
        }

        var createMatch = CreatePrimitivePattern.Match(text);
        if (createMatch.Success)
        {
            var rawPrimitive = createMatch.Groups["primitive"].Value.Trim().ToLowerInvariant();
            var normalizedPrimitive = rawPrimitive == "cube" ? "box" : rawPrimitive;
            var command = new CadViewportCommand(
                CadViewportCommandKind.AddPrimitive,
                PrimitiveKind: normalizedPrimitive);

            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var moveMatch = MovePattern.Match(text);
        if (moveMatch.Success &&
            double.TryParse(
                moveMatch.Groups["distance"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var delta))
        {
            var axis = moveMatch.Groups["axis"].Value.Trim().ToLowerInvariant();
            var command = new CadViewportCommand(
                CadViewportCommandKind.MoveSelected,
                Axis: axis,
                Distance: delta);

            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        if (DeletePattern.IsMatch(text))
        {
            var command = new CadViewportCommand(CadViewportCommandKind.DeleteSelected);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        if (FocusPattern.IsMatch(text))
        {
            var command = new CadViewportCommand(CadViewportCommandKind.FocusCamera);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var selectPlaneMatch = SelectPlanePattern.Match(text);
        if (selectPlaneMatch.Success)
        {
            var planeRaw = FirstNonEmpty(selectPlaneMatch.Groups["plane"].Value, selectPlaneMatch.Groups["plane2"].Value);
            var plane = NormalizePlane(planeRaw);
            if (!string.IsNullOrWhiteSpace(plane))
            {
                var command = new CadViewportCommand(CadViewportCommandKind.SelectPlane, Plane: plane);
                return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
            }
        }

        var startSketchMatch = StartSketchPattern.Match(text);
        if (startSketchMatch.Success)
        {
            var plane = NormalizePlane(startSketchMatch.Groups["plane"].Value);
            var command = new CadViewportCommand(CadViewportCommandKind.StartSketch, Plane: plane);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        if (FinishSketchPattern.IsMatch(text))
        {
            var command = new CadViewportCommand(CadViewportCommandKind.FinishSketch);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        if (CancelSketchPattern.IsMatch(text))
        {
            var command = new CadViewportCommand(CadViewportCommandKind.CancelSketch);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        if (TryParseSketchCoordinateCommand(text, out var coordinateCommand))
        {
            return coordinateCommand;
        }

        var sketchToolMatch = SketchToolPattern.Match(text);
        if (sketchToolMatch.Success)
        {
            var sketchTool = NormalizeSketchTool(FirstNonEmpty(sketchToolMatch.Groups["tool"].Value, sketchToolMatch.Groups["tool2"].Value));
            if (!string.IsNullOrWhiteSpace(sketchTool))
            {
                var command = new CadViewportCommand(CadViewportCommandKind.SetSketchTool, SketchTool: sketchTool);
                return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
            }
        }

        var constraintMatch = ConstraintPattern.Match(text);
        if (constraintMatch.Success)
        {
            var constraintName = NormalizeSketchConstraint(FirstNonEmpty(
                constraintMatch.Groups["constraint"].Value,
                constraintMatch.Groups["constraint2"].Value,
                constraintMatch.Groups["constraint3"].Value));
            if (!string.IsNullOrWhiteSpace(constraintName))
            {
                var command = new CadViewportCommand(
                    CadViewportCommandKind.ApplySketchConstraint,
                    ConstraintName: constraintName);
                return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
            }
        }

        var extrudeMatch = ExtrudePattern.Match(text);
        if (extrudeMatch.Success)
        {
            var tail = extrudeMatch.Groups["tail"].Value;
            if (!IsRecognizedExtrudeTail(tail))
            {
                return CadCommandParseResult.NotMatched(
                    "Extrude command was not recognized. Try: extrude 8, extrude reverse 8, extrude join 8, extrude cut 8, or extrude symmetric 8.");
            }

            var distance = ParseExtrudeDistance(tail);
            if (Regex.IsMatch(tail, @"\b(reverse|reversed|opposite)\b", RegexOptions.IgnoreCase))
            {
                distance = -Math.Abs(distance);
            }

            var command = new CadViewportCommand(
                CadViewportCommandKind.ExtrudeSketch,
                Distance: distance,
                ExtrudeOp: ParseExtrudeOperation(tail));
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var revolveMatch = RevolvePattern.Match(text);
        if (revolveMatch.Success)
        {
            var angle = ParseOrDefault(revolveMatch.Groups["angle"].Value, 360, min: 5, max: 360);
            var axis = string.Equals(revolveMatch.Groups["axis"].Value, "x", StringComparison.OrdinalIgnoreCase) ? "x" : "y";
            var command = new CadViewportCommand(CadViewportCommandKind.RevolveSketch, Distance: angle, Axis: axis);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var filletMatch = FilletPattern.Match(text);
        if (filletMatch.Success)
        {
            var radius = ParseOrDefault(filletMatch.Groups["radius"].Value, 2.0, min: 0.1, max: 100);
            var command = new CadViewportCommand(CadViewportCommandKind.FilletBody, Distance: radius);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var shellMatch = ShellPattern.Match(text);
        if (shellMatch.Success)
        {
            var thickness = ParseOrDefault(shellMatch.Groups["thickness"].Value, 2.0, min: 0.1, max: 500);
            var command = new CadViewportCommand(CadViewportCommandKind.ShellBody, Distance: thickness);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var mirrorMatch = MirrorPattern.Match(text);
        if (mirrorMatch.Success)
        {
            var axisRaw = mirrorMatch.Groups["axis"].Value.ToLowerInvariant();
            var axis = string.IsNullOrWhiteSpace(axisRaw) ? "x" : axisRaw;
            var command = new CadViewportCommand(CadViewportCommandKind.MirrorBody, Axis: axis);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var chamferMatch = ChamferPattern.Match(text);
        if (chamferMatch.Success)
        {
            var distance = ParseOrDefault(chamferMatch.Groups["distance"].Value, 1.0, min: 0.1, max: 100);
            var command = new CadViewportCommand(CadViewportCommandKind.ChamferBody, Distance: distance);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var linearPatternMatch = LinearPatternPattern.Match(text);
        if (linearPatternMatch.Success)
        {
            var count = ParseOrDefault(linearPatternMatch.Groups["count"].Value, 2, min: 2, max: 100);
            var spacing = ParseOrDefault(linearPatternMatch.Groups["spacing"].Value, 10, min: 0.1, max: 10000);
            var axisRaw = linearPatternMatch.Groups["axis"].Value.ToLowerInvariant();
            var axis = string.IsNullOrWhiteSpace(axisRaw) ? "x" : axisRaw;
            var command = new CadViewportCommand(CadViewportCommandKind.LinearPatternBody, Distance: count, U: spacing, Axis: axis);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var circularPatternMatch = CircularPatternPattern.Match(text);
        if (circularPatternMatch.Success)
        {
            var count = ParseOrDefault(circularPatternMatch.Groups["count"].Value, 4, min: 2, max: 100);
            var angle = ParseOrDefault(circularPatternMatch.Groups["angle"].Value, 360, min: 5, max: 360);
            var axisRaw = circularPatternMatch.Groups["axis"].Value.ToLowerInvariant();
            var axis = string.IsNullOrWhiteSpace(axisRaw) ? "y" : axisRaw;
            var command = new CadViewportCommand(CadViewportCommandKind.CircularPatternBody, Distance: count, U: angle, Axis: axis);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var holeMatch = HolePattern.Match(text);
        if (holeMatch.Success)
        {
            var depth = ParseOrDefault(holeMatch.Groups["depth"].Value, 10, min: 0.5, max: 10000);
            var command = new CadViewportCommand(CadViewportCommandKind.HoleBody, Distance: depth);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        var booleanMatch = BooleanPattern.Match(text);
        if (booleanMatch.Success)
        {
            var op = booleanMatch.Groups["op"].Value.ToLowerInvariant();
            var kind = op is "union" or "join"
                ? CadViewportCommandKind.BooleanUnion
                : op is "subtract" or "cut"
                    ? CadViewportCommandKind.BooleanSubtract
                    : CadViewportCommandKind.BooleanIntersect;
            var command = new CadViewportCommand(kind);
            return CadCommandParseResult.Success(command, $"Command parsed: {command.Describe()}");
        }

        return CadCommandParseResult.NotMatched("No local CAD command recognized.");
    }

    private static bool TryParseSketchCoordinateCommand(string text, out CadCommandParseResult result)
    {
        var pointMatch = PointCoordinatePattern.Match(text);
        if (pointMatch.Success &&
            TryReadPoint(pointMatch, "x", "y", out var point))
        {
            result = BuildPlacementCommand("Point", [point], "point");
            return true;
        }

        var lineMatch = LineCoordinatePattern.Match(text);
        if (lineMatch.Success &&
            TryReadPoint(lineMatch, "x1", "y1", out var lineStart) &&
            TryReadPoint(lineMatch, "x2", "y2", out var lineEnd))
        {
            result = BuildPlacementCommand("Line", [lineStart, lineEnd], "line");
            return true;
        }

        var rectangleMatch = RectangleCoordinatePattern.Match(text);
        if (rectangleMatch.Success &&
            TryReadPoint(rectangleMatch, "x1", "y1", out var rectStart) &&
            TryReadPoint(rectangleMatch, "x2", "y2", out var rectEnd))
        {
            result = BuildPlacementCommand("Rectangle", [rectStart, rectEnd], "rectangle");
            return true;
        }

        var circleMatch = CircleCoordinatePattern.Match(text);
        if (circleMatch.Success &&
            TryReadPoint(circleMatch, "cx", "cy", out var center) &&
            TryReadNumber(circleMatch.Groups["r"].Value, out var radius))
        {
            if (radius <= 0d)
            {
                result = CadCommandParseResult.NotMatched("Circle radius must be greater than zero.");
                return true;
            }

            var radiusPoint = (X: center.X + radius, Y: center.Y);
            result = BuildPlacementCommand("Circle", [center, radiusPoint], "circle");
            return true;
        }

        var arcMatch = ArcCoordinatePattern.Match(text);
        if (arcMatch.Success &&
            TryReadPoint(arcMatch, "x1", "y1", out var arcStart) &&
            TryReadPoint(arcMatch, "x2", "y2", out var arcEnd) &&
            TryReadPoint(arcMatch, "x3", "y3", out var arcThrough))
        {
            result = BuildPlacementCommand("Arc", [arcStart, arcEnd, arcThrough], "arc");
            return true;
        }

        result = CadCommandParseResult.NotMatched("No coordinate sketch command recognized.");
        return false;
    }

    private static CadCommandParseResult BuildPlacementCommand(
        string sketchTool,
        IReadOnlyList<(double X, double Y)> points,
        string label)
    {
        var commands = new List<CadViewportCommand>(points.Count + 1)
        {
            new(CadViewportCommandKind.SetSketchTool, SketchTool: sketchTool)
        };

        foreach (var point in points)
        {
            commands.Add(new CadViewportCommand(CadViewportCommandKind.PlaceSketchAt, U: point.X, V: point.Y));
        }

        return CadCommandParseResult.Success(commands, $"Command parsed: place {label} from coordinates.");
    }

    private static bool TryReadPoint(Match match, string xGroup, string yGroup, out (double X, double Y) point)
    {
        if (TryReadNumber(match.Groups[xGroup].Value, out var x) &&
            TryReadNumber(match.Groups[yGroup].Value, out var y))
        {
            point = (x, y);
            return true;
        }

        point = default;
        return false;
    }

    private static bool TryReadNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string NormalizeCommandText(string input)
    {
        var text = input.Trim();
        text = Regex.Replace(text, @"^\s*(?:[-*]\s+|\d+[\.)]\s+)", string.Empty);
        text = Regex.Replace(text, @"^(?:please\s+|can\s+you\s+|could\s+you\s+)", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim(' ', '.', ',');
    }

    private static double ParseOrDefault(string text, double fallback, double min, double max)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static bool IsRecognizedExtrudeTail(string text)
    {
        var remaining = Regex.Replace(text, NumberPattern, " ", RegexOptions.IgnoreCase);
        remaining = Regex.Replace(
            remaining,
            @"\b(profile|selected|sketch|by|to|as|distance|depth|mm|normal|reverse|reversed|opposite|new|body|newbody|join|add|union|cut|subtract|remove|symmetric|midplane|mid|plane)\b",
            " ",
            RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(remaining);
    }

    private static double ParseExtrudeDistance(string text)
    {
        var match = Regex.Match(text, NumberPattern, RegexOptions.IgnoreCase);
        if (!match.Success ||
            !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return 8.0;
        }

        var magnitude = Math.Clamp(Math.Abs(parsed), 0.2, 5000);
        return parsed < 0d ? -magnitude : magnitude;
    }

    private static string ParseExtrudeOperation(string text)
    {
        var normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ");
        if (Regex.IsMatch(normalized, @"\b(symmetric|midplane|mid plane)\b", RegexOptions.IgnoreCase))
        {
            return "Symmetric";
        }

        if (Regex.IsMatch(normalized, @"\b(join|add|union)\b", RegexOptions.IgnoreCase))
        {
            return "Join";
        }

        if (Regex.IsMatch(normalized, @"\b(cut|subtract|remove)\b", RegexOptions.IgnoreCase))
        {
            return "Cut";
        }

        return "NewBody";
    }

    private static string? NormalizePlane(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "top" => "Top",
            "front" => "Front",
            "right" => "Right",
            _ => null
        };
    }

    private static string? NormalizeSketchTool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = Regex.Replace(raw.Trim().ToLowerInvariant(), @"\s+", " ");
        return normalized switch
        {
            "line" => "Line",
            "rectangle" => "Rectangle",
            "circle" => "Circle",
            "arc" => "Arc",
            "point" => "Point",
            "polygon" => "Polygon",
            "slot" => "Slot",
            "spline" => "Spline",
            "mirror" => "Mirror",
            "trim" => "Trim",
            "offset" => "Offset",
            "fillet2d" or "2d fillet" or "fillet 2d" or "sketch fillet" => "Fillet2d",
            "transform" => "Transform",
            "rotate" => "Rotate",
            "angle dimension" or "angle dim" or "dimension" => "AngleDimension",
            _ => null
        };
    }

    private static string? NormalizeSketchConstraint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "horizontal" => "Horizontal",
            "vertical" => "Vertical",
            "coincident" => "Coincident",
            "equal" => "Equal",
            "fix" or "fixed" => "Fixed",
            "tangent" => "Tangent",
            "parallel" => "Parallel",
            "perpendicular" => "Perpendicular",
            "concentric" => "Concentric",
            _ => null
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
