using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

internal sealed class MakerTemplateManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("previewSummary")]
    public string PreviewSummary { get; set; } = string.Empty;

    [JsonPropertyName("builder")]
    public string Builder { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<MakerTemplateParameterManifest> Parameters { get; set; } = [];
}

internal sealed class MakerTemplateParameterManifest
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("defaultValue")]
    public double DefaultValue { get; set; }

    [JsonPropertyName("minValue")]
    public double MinValue { get; set; }

    [JsonPropertyName("maxValue")]
    public double MaxValue { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "mm";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public static class MakerTemplateLibrary
{
    private static readonly Regex TemplateInvocationPattern = new(
        @"^(?:use\s+)?template\s+(?<id>[a-z0-9\-]+)(?<args>(?:\s+[a-zA-Z][a-zA-Z0-9]*\s*=\s*[+-]?\d+(?:\.\d+)?)*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AssignmentPattern = new(
        @"(?<key>[a-zA-Z][a-zA-Z0-9]*)\s*=\s*(?<value>[+-]?\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Dictionary<string, Func<IReadOnlyDictionary<string, double>, string>> Builders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["mounting-plate"] = BuildMountingPlate,
            ["washer"] = BuildWasher,
            ["spacer"] = BuildSpacer,
            ["l-bracket"] = BuildLBracket,
            ["fan-adapter"] = BuildFanAdapter,
            ["cable-clip"] = BuildCableClip
        };

    public static IReadOnlyList<MakerTemplateDefinition> All { get; } = LoadTemplates();

    public static MakerTemplateDefinition? Find(string id) =>
        All.FirstOrDefault(item => string.Equals(item.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool TryExpandInvocation(string text, out string expanded)
    {
        expanded = string.Empty;
        var match = TemplateInvocationPattern.Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var template = Find(match.Groups["id"].Value);
        if (template is null)
        {
            return false;
        }

        var values = template.Parameters.ToDictionary(
            item => item.Key,
            item => item.DefaultValue,
            StringComparer.OrdinalIgnoreCase);

        foreach (Match assignment in AssignmentPattern.Matches(match.Groups["args"].Value))
        {
            var key = assignment.Groups["key"].Value;
            if (!double.TryParse(assignment.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                continue;
            }

            if (values.ContainsKey(key))
            {
                values[key] = parsed;
            }
        }

        expanded = template.BuildCommand(values);
        return true;
    }

    private static IReadOnlyList<MakerTemplateDefinition> LoadTemplates()
    {
        var manifestFiles = EnumerateManifestFiles().ToList();
        if (manifestFiles.Count == 0)
        {
            return BuildFallbackTemplates();
        }

        var templates = new List<MakerTemplateDefinition>();
        foreach (var file in manifestFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize<MakerTemplateManifest>(json, JsonOptions);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
                {
                    continue;
                }

                if (!Builders.TryGetValue(manifest.Builder, out var builder) &&
                    !Builders.TryGetValue(manifest.Id, out builder))
                {
                    continue;
                }

                templates.Add(new MakerTemplateDefinition(
                    manifest.Id,
                    string.IsNullOrWhiteSpace(manifest.DisplayName) ? manifest.Id : manifest.DisplayName,
                    string.IsNullOrWhiteSpace(manifest.Category) ? "Templates" : manifest.Category,
                    manifest.Description,
                    manifest.Tags,
                    manifest.Parameters
                        .Select(item => new MakerTemplateParameter(
                            item.Key,
                            string.IsNullOrWhiteSpace(item.DisplayName) ? item.Key : item.DisplayName,
                            item.DefaultValue,
                            item.MinValue,
                            item.MaxValue,
                            string.IsNullOrWhiteSpace(item.Unit) ? "mm" : item.Unit,
                            item.Description))
                        .ToArray(),
                    manifest.PreviewSummary,
                    builder));
            }
            catch
            {
                // Keep the app resilient if a contributor drops in a malformed template manifest.
            }
        }

        return templates.Count > 0 ? templates : BuildFallbackTemplates();
    }

    private static IEnumerable<string> EnumerateManifestFiles()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var templatesDir = Path.Combine(current.FullName, "PartLibrary", "Templates");
            if (Directory.Exists(templatesDir))
            {
                return Directory.EnumerateFiles(templatesDir, "template.json", SearchOption.AllDirectories);
            }

            current = current.Parent;
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<MakerTemplateDefinition> BuildFallbackTemplates()
    {
        return
        [
            Fallback(
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
                BuildMountingPlate),

            Fallback(
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
                BuildWasher),

            Fallback(
                "spacer",
                "Spacer / Standoff",
                "Disks & spacers",
                "Round standoff body generated as a simple cylinder with optional center hole.",
                ["spacer", "standoff", "hardware"],
                [
                    P("outerDiameter", "Outer Diameter", 12, 4, 80, "mm", "Outside diameter"),
                    P("innerDiameter", "Inner Diameter", 4, 0, 40, "mm", "Center bore diameter"),
                    P("height", "Height", 16, 2, 120, "mm", "Spacer height"),
                    P("chamfer", "Chamfer", 0.5, 0, 8, "mm", "Simple edge break")
                ],
                "Quick standoff for electronics and panel spacing.",
                BuildSpacer),

            Fallback(
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
                BuildLBracket),

            Fallback(
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
                BuildFanAdapter),

            Fallback(
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
                BuildCableClip)
        ];
    }

    private static MakerTemplateDefinition Fallback(
        string id,
        string displayName,
        string category,
        string description,
        IReadOnlyList<string> tags,
        IReadOnlyList<MakerTemplateParameter> parameters,
        string previewSummary,
        Func<IReadOnlyDictionary<string, double>, string> builder) =>
        new(id, displayName, category, description, tags, parameters, previewSummary, builder);

    private static string BuildMountingPlate(IReadOnlyDictionary<string, double> values)
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
    }

    private static string BuildWasher(IReadOnlyDictionary<string, double> values)
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
    }

    private static string BuildSpacer(IReadOnlyDictionary<string, double> values)
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
    }

    private static string BuildLBracket(IReadOnlyDictionary<string, double> values)
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
    }

    private static string BuildFanAdapter(IReadOnlyDictionary<string, double> values)
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
    }

    private static string BuildCableClip(IReadOnlyDictionary<string, double> values)
    {
        var cableDiameter = V(values, "cableDiameter");
        var clipWidth = V(values, "clipWidth");
        var wallThickness = V(values, "wallThickness");
        var overall = Math.Max(cableDiameter + wallThickness * 2d, wallThickness * 2d + 2d);
        var depth = Math.Max(clipWidth, wallThickness * 2d + 4d);
        return $"create box {N(overall)}x{N(depth)}x{N(clipWidth)}; fillet {N(Math.Min(wallThickness, 3d))}";
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
