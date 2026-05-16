using System.Globalization;
using System.Text;

namespace My3DApp.AvaloniaApp.Services;

public sealed record MakerTemplateParameter(
    string Key,
    string DisplayName,
    double DefaultValue,
    double MinValue,
    double MaxValue,
    string Unit,
    string Description);

public sealed record MakerTemplateDefinition(
    string Id,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MakerTemplateParameter> Parameters,
    string PreviewSummary,
    Func<IReadOnlyDictionary<string, double>, string> BuildCommand)
{
    public string TagSummary => string.Join(" · ", Tags);
}

public static class MakerTemplateLibrary
{
    public static IReadOnlyList<MakerTemplateDefinition> All { get; } = BuildTemplates();

    public static MakerTemplateDefinition? Find(string id) =>
        All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<MakerTemplateDefinition> BuildTemplates()
    {
        return
        [
            new(
                "mounting-plate",
                "Mounting Plate",
                "Plates & mounts",
                "Rectangular maker plate with optional symmetric hole pattern.",
                ["plate", "mount", "panel", "maker"],
                [
                    P("width", "Width", 80, 20, 240, "mm", "Overall width"),
                    P("height", "Height", 50, 20, 240, "mm", "Overall height"),
                    P("thickness", "Thickness", 4, 1, 25, "mm", "Plate thickness"),
                    P("cornerRadius", "Corner Radius", 4, 0, 30, "mm", "Rounded corner radius"),
                    P("holeDiameter", "Hole Diameter", 5, 0, 20, "mm", "Fastener hole diameter"),
                    P("holeMargin", "Hole Margin", 10, 2, 40, "mm", "Hole offset from edges"),
                    P("holeCount", "Hole Count", 4, 0, 4, "count", "0, 2, or 4 holes")
                ],
                "Simple printable mounting plate with lightweight hole support.",
                values =>
                {
                    var width = V(values, "width");
                    var height = V(values, "height");
                    var thickness = V(values, "thickness");
                    var holeDiameter = V(values, "holeDiameter");
                    var holeCount = ClampCount(V(values, "holeCount"));
                    var holeDepth = Math.Max(1, thickness);

                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; rectangle 0 0 {N(width)} {N(height)}; finish sketch; extrude {N(thickness)}");
                    if (holeCount > 0 && holeDiameter > 0.1d)
                    {
                        for (var i = 0; i < holeCount; i++)
                        {
                            sb.Append($"; hole depth {N(holeDepth)}");
                        }
                    }

                    return sb.ToString();
                }),

            new(
                "washer",
                "Washer",
                "Disks & spacers",
                "Printable spacer/washer blank with a fast center hole operation.",
                ["washer", "spacer", "round", "hardware"],
                [
                    P("outerDiameter", "Outer Diameter", 24, 6, 120, "mm", "Outside diameter"),
                    P("innerDiameter", "Inner Diameter", 8, 0, 60, "mm", "Center clearance diameter"),
                    P("thickness", "Thickness", 2.5, 0.5, 25, "mm", "Part thickness")
                ],
                "Fast hardware spacer for printer and enclosure builds.",
                values =>
                {
                    var outerRadius = Math.Max(V(values, "outerDiameter") / 2d, 1d);
                    var innerDiameter = Math.Max(V(values, "innerDiameter"), 0d);
                    var thickness = V(values, "thickness");

                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(outerRadius)}; finish sketch; extrude {N(thickness)}");
                    if (innerDiameter > 0.1d)
                    {
                        sb.Append($"; hole depth {N(Math.Max(thickness, 1d))}");
                    }

                    return sb.ToString();
                }),

            new(
                "spacer",
                "Spacer / Standoff",
                "Disks & spacers",
                "Round standoff body generated as a simple cylinder with optional center hole.",
                ["spacer", "standoff", "standoff", "hardware"],
                [
                    P("outerDiameter", "Outer Diameter", 12, 4, 80, "mm", "Outside diameter"),
                    P("innerDiameter", "Inner Diameter", 4, 0, 40, "mm", "Center bore diameter"),
                    P("height", "Height", 16, 2, 120, "mm", "Spacer height"),
                    P("chamfer", "Chamfer", 0.5, 0, 8, "mm", "Simple edge break")
                ],
                "Quick standoff for electronics and panel spacing.",
                values =>
                {
                    var outerRadius = Math.Max(V(values, "outerDiameter") / 2d, 1d);
                    var innerDiameter = Math.Max(V(values, "innerDiameter"), 0d);
                    var height = V(values, "height");
                    var chamfer = V(values, "chamfer");

                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(outerRadius)}; finish sketch; extrude {N(height)}");
                    if (innerDiameter > 0.1d)
                    {
                        sb.Append($"; hole depth {N(Math.Max(height, 1d))}");
                    }

                    if (chamfer > 0.05d)
                    {
                        sb.Append($"; chamfer {N(chamfer)}");
                    }

                    return sb.ToString();
                }),

            new(
                "l-bracket",
                "L-Bracket",
                "Brackets",
                "General-purpose maker bracket built from two joined plates.",
                ["bracket", "support", "angle", "mount"],
                [
                    P("width", "Width", 60, 10, 200, "mm", "Bracket width"),
                    P("height", "Height", 50, 10, 200, "mm", "Bracket upright height"),
                    P("depth", "Depth", 40, 10, 200, "mm", "Bracket base depth"),
                    P("thickness", "Thickness", 4, 1, 30, "mm", "Wall thickness"),
                    P("holeDiameter", "Hole Diameter", 5, 0, 18, "mm", "Mounting hole diameter"),
                    P("holeCount", "Hole Count", 2, 0, 4, "count", "Approximate hole feature count")
                ],
                "Starter support bracket for mounts and enclosures.",
                values =>
                {
                    var width = V(values, "width");
                    var height = V(values, "height");
                    var depth = V(values, "depth");
                    var thickness = V(values, "thickness");
                    var holeCount = ClampCount(V(values, "holeCount"));
                    var holeDiameter = Math.Max(V(values, "holeDiameter"), 0d);
                    var sb = new StringBuilder();
                    sb.Append($"recipe bracket {N(width)} {N(depth)} {N(height)} {N(thickness)}");
                    if (holeCount > 0 && holeDiameter > 0.1d)
                    {
                        for (var i = 0; i < holeCount; i++)
                        {
                            sb.Append($"; hole depth {N(Math.Max(thickness, 1d))}");
                        }
                    }

                    return sb.ToString();
                }),

            new(
                "fan-adapter",
                "Fan Adapter Plate",
                "Plates & mounts",
                "Simplified fan plate sized for common square fan footprints.",
                ["fan", "adapter", "plate", "cooling"],
                [
                    P("fanSize", "Fan Size", 80, 40, 140, "mm", "Nominal fan body size"),
                    P("thickness", "Thickness", 3, 1, 20, "mm", "Plate thickness"),
                    P("screwHoleDiameter", "Screw Hole Diameter", 4.5, 0, 12, "mm", "Screw hole diameter"),
                    P("centerOpeningDiameter", "Center Opening Diameter", 60, 10, 120, "mm", "Center airflow opening")
                ],
                "Good early template for cooling mounts and printer mods.",
                values =>
                {
                    var fanSize = V(values, "fanSize");
                    var thickness = V(values, "thickness");
                    var screwHoleDiameter = Math.Max(V(values, "screwHoleDiameter"), 0d);
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; rectangle 0 0 {N(fanSize)} {N(fanSize)}; finish sketch; extrude {N(thickness)}");
                    if (screwHoleDiameter > 0.1d)
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            sb.Append($"; hole depth {N(Math.Max(thickness, 1d))}");
                        }
                    }

                    return sb.ToString();
                }),

            new(
                "cable-clip",
                "Cable Clip",
                "Clips & routing",
                "Simple printable clip block for cable management.",
                ["clip", "cable", "routing", "maker"],
                [
                    P("cableDiameter", "Cable Diameter", 6, 2, 30, "mm", "Target cable size"),
                    P("clipWidth", "Clip Width", 14, 4, 80, "mm", "Body width"),
                    P("wallThickness", "Wall Thickness", 2.5, 1, 12, "mm", "Wall thickness"),
                    P("openingGap", "Opening Gap", 4, 1, 20, "mm", "Entry slot opening")
                ],
                "Practical cable organizer template for enclosures and printer frames.",
                values =>
                {
                    var cableDiameter = V(values, "cableDiameter");
                    var clipWidth = V(values, "clipWidth");
                    var wallThickness = V(values, "wallThickness");
                    var overall = Math.Max(cableDiameter + wallThickness * 2d, wallThickness * 2d + 2d);
                    var depth = Math.Max(clipWidth, wallThickness * 2d + 4d);
                    return $"create box {N(overall)}x{N(depth)}x{N(clipWidth)}; fillet {N(Math.Min(wallThickness, 3d))}";
                })
        ];
    }

    private static MakerTemplateParameter P(
        string key,
        string displayName,
        double defaultValue,
        double minValue,
        double maxValue,
        string unit,
        string description) =>
        new(key, displayName, defaultValue, minValue, maxValue, unit, description);

    private static double V(IReadOnlyDictionary<string, double> values, string key) =>
        values.TryGetValue(key, out var value) ? value : 0d;

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static int ClampCount(double raw)
    {
        var rounded = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return rounded switch
        {
            <= 0 => 0,
            <= 2 => 2,
            _ => 4
        };
    }
}
