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

public sealed record MakerTemplatePreset(
    string Name,
    string Description,
    IReadOnlyDictionary<string, double> Values);

public sealed record MakerTemplateDefinition(
    string Id,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MakerTemplateParameter> Parameters,
    string PreviewSummary,
    Func<IReadOnlyDictionary<string, double>, string> BuildCommand,
    IReadOnlyList<MakerTemplatePreset> Presets)
{
    public string TagSummary => string.Join(" · ", Tags);

    public string Signature =>
        $"template {Id} {string.Join(' ', Parameters.Select(p => $"{p.Key}=<{p.Unit}>"))}";
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

    [JsonPropertyName("scriptFile")]
    public string ScriptFile { get; set; } = string.Empty;

    [JsonPropertyName("aclScript")]
    public string AclScript { get; set; } = string.Empty;

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
        @"^(?:(?:use|make|generate)\s+)?(?:template|part)\s+(?<id>[a-z0-9\-]+)(?<args>(?:\s+[a-zA-Z][a-zA-Z0-9]*\s*=\s*[+-]?\d+(?:\.\d+)?)*)\s*$",
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
            ["cable-clip"] = BuildCableClip,
            ["din-rail-clip"] = BuildDinRailClip,
            ["t-slot-nut"] = BuildTSlotNut,
            ["box-enclosure"] = BuildBoxEnclosure,
            ["hinge-bracket"] = BuildHingeBracket,
            ["pcb-tray"] = BuildPcbTray,
            ["simple-box"] = BuildSimpleBox,
            ["lid"] = BuildLid,
            ["gear"] = BuildGear,
            ["snap-fit-case"] = BuildSnapFitCase,
            ["vaulted-clip"] = BuildVaultedClip,
            ["heat-set-insert-boss"] = BuildHeatSetInsertBoss,
            ["gridfinity-bin"] = BuildGridfinityBin
        };

    public static IReadOnlyList<MakerTemplateDefinition> All { get; } = LoadTemplates();

    public static IReadOnlyList<string> Categories =>
        All.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();

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

                var templateDirectory = Path.GetDirectoryName(file) ?? string.Empty;
                Func<IReadOnlyDictionary<string, double>, string>? builder = null;
                if (!string.IsNullOrWhiteSpace(manifest.ScriptFile) || !string.IsNullOrWhiteSpace(manifest.AclScript))
                {
                    var scriptText = LoadTemplateScript(manifest, templateDirectory);
                    if (!string.IsNullOrWhiteSpace(scriptText))
                    {
                        builder = CreateAclTemplateBuilder(manifest, scriptText);
                    }
                }

                if (builder is null &&
                    !Builders.TryGetValue(manifest.Builder, out builder) &&
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
                    builder,
                    BuildPresetsForTemplate(manifest.Id)));
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

    private static string LoadTemplateScript(MakerTemplateManifest manifest, string templateDirectory)
    {
        if (!string.IsNullOrWhiteSpace(manifest.AclScript))
        {
            return manifest.AclScript.Trim();
        }

        if (string.IsNullOrWhiteSpace(manifest.ScriptFile) || string.IsNullOrWhiteSpace(templateDirectory))
        {
            return string.Empty;
        }

        var scriptPath = Path.Combine(templateDirectory, manifest.ScriptFile);
        return File.Exists(scriptPath) ? File.ReadAllText(scriptPath) : string.Empty;
    }

    private static Func<IReadOnlyDictionary<string, double>, string> CreateAclTemplateBuilder(
        MakerTemplateManifest manifest,
        string scriptText)
    {
        var parameters = manifest.Parameters
            .Select(item => item.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values =>
        {
            var preamble = new StringBuilder();
            foreach (var key in parameters)
            {
                var value = values.TryGetValue(key, out var current)
                    ? current
                    : manifest.Parameters.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))?.DefaultValue ?? 0d;
                preamble.Append("let ");
                preamble.Append(key);
                preamble.Append(" = ");
                preamble.Append(N(value));
                preamble.AppendLine();
            }

            preamble.AppendLine();
            preamble.Append(scriptText.Trim());
            if (!CadScriptLibrary.TryExpandSequence(preamble.ToString(), out var expanded, out var error))
            {
                throw new InvalidOperationException(
                    $"ACL template '{manifest.Id}' failed to expand: {error ?? "unknown script error"}");
            }

            return expanded;
        };
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
                BuildCableClip),

            Fallback(
                "din-rail-clip",
                "DIN Rail Clip",
                "Clips & routing",
                "Parametric 35mm DIN rail mounting clip for electronics panels and enclosures.",
                ["din", "rail", "clip", "electronics", "panel"],
                [
                    P("clipHeight", "Clip Height", 28, 12, 80, "mm", "Overall clip height"),
                    P("railWidth", "Rail Width", 35, 20, 60, "mm", "DIN rail width (standard: 35)"),
                    P("wallThickness", "Wall Thickness", 3, 1.5, 10, "mm", "Clip body wall thickness"),
                    P("lipDepth", "Lip Depth", 7, 4, 14, "mm", "Rail retention lip depth"),
                    P("screwHoleDiameter", "Screw Hole Dia.", 3.5, 0, 6, "mm", "Mounting screw hole (0=none)")
                ],
                "35mm DIN rail mounting clip for panel electronics.",
                BuildDinRailClip),

            Fallback(
                "t-slot-nut",
                "T-Slot Nut",
                "Fasteners & hardware",
                "Printable T-nut for standard 2020/2040/3030 aluminum extrusion T-slots.",
                ["t-nut", "t-slot", "extrusion", "2020", "hardware"],
                [
                    P("slotWidth", "Slot Width", 6, 4, 10, "mm", "Extrusion slot width (2020=6mm, 3030=8mm)"),
                    P("nutLength", "Nut Length", 20, 8, 60, "mm", "Nut body length"),
                    P("nutHeight", "Nut Height", 3, 1.5, 8, "mm", "Nut body thickness"),
                    P("holeDiameter", "Thread Hole Dia.", 3.2, 1.5, 6, "mm", "Center hole diameter"),
                    P("flangeWidth", "Flange Width", 10, 6, 20, "mm", "T-flange outer width")
                ],
                "Drop-in T-nut for aluminum extrusion frame construction.",
                BuildTSlotNut),

            Fallback(
                "box-enclosure",
                "Box Enclosure",
                "Enclosures",
                "Parametric rectangular enclosure shell for electronics project boxes.",
                ["box", "enclosure", "case", "electronics", "housing"],
                [
                    P("innerWidth", "Inner Width", 80, 20, 300, "mm", "Interior cavity width"),
                    P("innerHeight", "Inner Height", 50, 15, 200, "mm", "Interior cavity height"),
                    P("innerDepth", "Inner Depth", 40, 15, 200, "mm", "Interior cavity depth"),
                    P("wallThickness", "Wall Thickness", 3, 1.5, 10, "mm", "Shell wall thickness"),
                    P("cornerRadius", "Corner Radius", 3, 0, 15, "mm", "Exterior corner rounding")
                ],
                "Parametric project enclosure shell with hollow interior.",
                BuildBoxEnclosure),

            Fallback(
                "hinge-bracket",
                "Hinge Bracket",
                "Brackets",
                "Two-leaf printable hinge bracket for panels, lids, and folding mechanisms.",
                ["hinge", "bracket", "pivot", "door", "lid"],
                [
                    P("leafWidth", "Leaf Width", 30, 10, 100, "mm", "Width of each hinge leaf"),
                    P("leafLength", "Leaf Length", 40, 15, 150, "mm", "Length of each hinge leaf"),
                    P("thickness", "Thickness", 3, 1.5, 8, "mm", "Leaf plate thickness"),
                    P("pinDiameter", "Pin Diameter", 5, 2, 12, "mm", "Hinge pin outer diameter"),
                    P("knuckleCount", "Knuckle Count", 3, 2, 5, "count", "Number of knuckle cylinders")
                ],
                "Printable two-leaf hinge for doors and enclosure lids.",
                BuildHingeBracket),

            Fallback(
                "pcb-tray",
                "PCB Tray",
                "Electronics",
                "Parametric PCB mounting tray with corner standoffs for single-board computers and shields.",
                ["pcb", "tray", "sled", "electronics", "raspberry-pi", "arduino"],
                [
                    P("boardWidth", "Board Width", 85, 30, 200, "mm", "PCB width"),
                    P("boardDepth", "Board Depth", 56, 25, 200, "mm", "PCB depth"),
                    P("wallHeight", "Wall Height", 8, 3, 40, "mm", "Tray wall height"),
                    P("wallThickness", "Wall Thickness", 2.5, 1.5, 8, "mm", "Tray wall thickness"),
                    P("standoffHeight", "Standoff Height", 5, 2, 20, "mm", "Corner standoff height above floor"),
                    P("standoffDiameter", "Standoff Diameter", 6, 3, 15, "mm", "Corner standoff outer diameter"),
                    P("standoffHoleDia", "Standoff Hole Dia.", 2.7, 0, 5, "mm", "Standoff center hole (0=solid)")
                ],
                "PCB mounting tray with corner standoffs for electronics builds.",
                BuildPcbTray),

            Fallback(
                "simple-box",
                "Simple Box",
                "Enclosures",
                "Simple printable box body with optional open top and shell thickness.",
                ["box", "container", "bin", "simple", "maker"],
                [
                    P("width", "Width", 80, 20, 300, "mm", "Overall width"),
                    P("depth", "Depth", 60, 20, 300, "mm", "Overall depth"),
                    P("height", "Height", 40, 10, 240, "mm", "Overall height"),
                    P("wallThickness", "Wall Thickness", 3, 1.5, 12, "mm", "Wall thickness"),
                    P("openTop", "Open Top", 1, 0, 1, "toggle", "1=open top, 0=closed"),
                    P("cornerRadius", "Corner Radius", 2, 0, 12, "mm", "Corner softening")
                ],
                "Simple maker box for storage, covers, and quick fixtures.",
                BuildSimpleBox),

            Fallback(
                "lid",
                "Lid / Cover",
                "Enclosures",
                "Flat printable lid with shallow locating lip for boxes and trays.",
                ["lid", "cover", "cap", "box"],
                [
                    P("width", "Width", 80, 20, 300, "mm", "Overall width"),
                    P("depth", "Depth", 60, 20, 300, "mm", "Overall depth"),
                    P("thickness", "Thickness", 3, 1, 20, "mm", "Top thickness"),
                    P("lipHeight", "Lip Height", 4, 0, 25, "mm", "Locating lip height"),
                    P("tolerance", "Tolerance", 0.4, 0, 3, "mm", "Fit clearance")
                ],
                "Simple cover plate for boxes and electronics trays.",
                BuildLid),

            Fallback(
                "gear",
                "Spur Gear",
                "Mechanical",
                "Parametric involute spur gear with configurable teeth, module, bore, and thickness.",
                ["gear", "mechanical", "transmission", "motor", "robot"],
                [
                    P("teethCount", "Teeth Count", 20, 8, 80, "count", "Number of teeth on gear"),
                    P("module", "Module (mm)", 2, 0.5, 8, "mm", "Tooth size parameter (pitch dia = teeth × module)"),
                    P("thickness", "Thickness", 6, 1, 30, "mm", "Gear thickness"),
                    P("boreDiameter", "Bore Diameter", 5, 0, 40, "mm", "Center shaft hole (0=solid)"),
                    P("hubDiameter", "Hub Diameter", 14, 0, 60, "mm", "Center hub reinforcement (0=none)"),
                    P("hubHeight", "Hub Height", 4, 0, 20, "mm", "Hub face protrusion (0=none)")
                ],
                "Precision parametric spur gear for robot and mechanism projects.",
                BuildGear),

            Fallback(
                "snap-fit-case",
                "Snap-Fit Case",
                "Enclosures",
                "Two-part snap-fit enclosure with configurable snaps and inner volume for electronics.",
                ["case", "snap", "enclosure", "electronics", "box"],
                [
                    P("innerWidth", "Inner Width", 80, 30, 250, "mm", "Interior cavity width"),
                    P("innerDepth", "Inner Depth", 55, 25, 200, "mm", "Interior cavity depth"),
                    P("innerHeight", "Inner Height", 30, 10, 150, "mm", "Interior cavity height per half"),
                    P("wallThickness", "Wall Thickness", 2.5, 1.5, 8, "mm", "Shell wall thickness"),
                    P("snapCount", "Snap Count", 4, 2, 8, "count", "Snap-fit tabs per half"),
                    P("snapOverhang", "Snap Overhang", 1.2, 0.4, 3, "mm", "Snap clip overhang depth"),
                    P("tolerance", "Tolerance", 0.3, 0, 1.5, "mm", "Fit clearance for assembly")
                ],
                "Two-part snap-fit electronics enclosure for FDM printing.",
                BuildSnapFitCase),

            Fallback(
                "vaulted-clip",
                "Spring Vault Clip",
                "Clips & routing",
                "Spring-loaded vaulted cable clip with flexible arms for secure cable retention.",
                ["clip", "spring", "vault", "cable", "grip"],
                [
                    P("cableDiameter", "Cable Diameter", 8, 2, 30, "mm", "Target cable diameter"),
                    P("clipWidth", "Clip Width", 16, 6, 80, "mm", "Overall clip body width"),
                    P("armLength", "Arm Length", 18, 6, 50, "mm", "Spring arm reach length"),
                    P("wallThickness", "Wall Thickness", 2, 1, 8, "mm", "Arm and body thickness"),
                    P("gripOverlap", "Grip Overlap", 3, 0.5, 10, "mm", "Arm tip overlap past center"),
                    P("mountHoleDia", "Mount Hole Dia.", 3.5, 0, 8, "mm", "Mounting screw hole (0=none)")
                ],
                "Spring-arm vault clip with secure grip for cable management.",
                BuildVaultedClip),

            Fallback(
                "heat-set-insert-boss",
                "Heat-Set Insert Boss",
                "Fasteners & hardware",
                "Reinforced boss designed for heat-set threaded inserts in 3D-printed parts.",
                ["insert", "heat-set", "boss", "threaded", "fastener"],
                [
                    P("insertDiameter", "Insert OD", 5.5, 3, 12, "mm", "Heat-set insert outer diameter"),
                    P("insertDepth", "Insert Depth", 6, 2, 20, "mm", "Insert cavity depth"),
                    P("wallThickness", "Wall Thickness", 3, 1.5, 10, "mm", "Boss wall thickness"),
                    P("bossHeight", "Boss Height", 8, 3, 30, "mm", "Total boss height above surface"),
                    P("baseFillet", "Base Fillet", 1.5, 0, 6, "mm", "Transition fillet at base"),
                    P("taperAngle", "Taper Angle", 2, 0, 15, "deg", "Boss draft taper angle")
                ],
                "Structural boss optimized for heat-set threaded inserts.",
                BuildHeatSetInsertBoss),

            Fallback(
                "gridfinity-bin",
                "Gridfinity Storage Bin",
                "Enclosures",
                "Gridfinity-compatible modular storage bin with magnet holes and label tab.",
                ["gridfinity", "storage", "bin", "organizer", "modular"],
                [
                    P("gridX", "Grid Units (X)", 2, 1, 6, "units", "Width in 42mm grid units"),
                    P("gridY", "Grid Units (Y)", 1, 1, 6, "units", "Depth in 42mm grid units"),
                    P("binHeight", "Bin Height", 42, 21, 168, "mm", "Interior bin wall height"),
                    P("wallThickness", "Wall Thickness", 2, 1.2, 6, "mm", "Bin wall thickness"),
                    P("magnetHoles", "Magnet Holes", 4, 0, 8, "count", "Base magnet hole count"),
                    P("labelTab", "Label Tab", 1, 0, 1, "toggle", "1=include label tab, 0=flat top")
                ],
                "Gridfinity-compatible modular storage bin with magnet-ready base.",
                BuildGridfinityBin)
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
        new(id, displayName, category, description, tags, parameters, previewSummary, builder, BuildPresetsForTemplate(id));

    private static IReadOnlyList<MakerTemplatePreset> BuildPresetsForTemplate(string id)
    {
        return id.ToLowerInvariant() switch
        {
            "mounting-plate" =>
            [
                Preset("Printer mount", "Compact 3D-printer accessory plate.",
                    KV("width", 80), KV("height", 50), KV("thickness", 4), KV("cornerRadius", 4), KV("holeDiameter", 5), KV("holeMargin", 10), KV("holeCount", 4)),
                Preset("Panel plate", "Larger utility plate for enclosures and mounts.",
                    KV("width", 140), KV("height", 90), KV("thickness", 5), KV("cornerRadius", 6), KV("holeDiameter", 6), KV("holeMargin", 14), KV("holeCount", 4))
            ],
            "washer" =>
            [
                Preset("M4 washer", "Small hardware washer.",
                    KV("outerDiameter", 12), KV("innerDiameter", 4.3), KV("thickness", 1.6)),
                Preset("M8 spacer", "Larger printable washer.",
                    KV("outerDiameter", 24), KV("innerDiameter", 8.4), KV("thickness", 3))
            ],
            "spacer" =>
            [
                Preset("PCB standoff", "Short spacer for electronics.",
                    KV("outerDiameter", 8), KV("innerDiameter", 3.2), KV("height", 8), KV("chamfer", 0.5)),
                Preset("Frame spacer", "Longer spacer for brackets and panels.",
                    KV("outerDiameter", 14), KV("innerDiameter", 5.2), KV("height", 20), KV("chamfer", 0.8))
            ],
            "l-bracket" =>
            [
                Preset("Shelf bracket", "Simple right-angle support.",
                    KV("width", 60), KV("height", 60), KV("depth", 40), KV("thickness", 4), KV("holeDiameter", 5), KV("holeCount", 2)),
                Preset("Heavy mount", "Larger support bracket for machines and enclosures.",
                    KV("width", 100), KV("height", 80), KV("depth", 60), KV("thickness", 6), KV("holeDiameter", 6), KV("holeCount", 4))
            ],
            "fan-adapter" =>
            [
                Preset("40 mm fan", "Compact blower or electronics fan plate.",
                    KV("fanSize", 40), KV("thickness", 3), KV("screwHoleDiameter", 3.2), KV("centerOpeningDiameter", 28)),
                Preset("120 mm fan", "Large cooling plate for case or enclosure airflow.",
                    KV("fanSize", 120), KV("thickness", 4), KV("screwHoleDiameter", 4.5), KV("centerOpeningDiameter", 102))
            ],
            "cable-clip" =>
            [
                Preset("Sensor wire", "Small clip for signal cable routing.",
                    KV("cableDiameter", 4), KV("clipWidth", 12), KV("wallThickness", 2), KV("openingGap", 3)),
                Preset("Power lead", "Wider clip for thicker harness runs.",
                    KV("cableDiameter", 8), KV("clipWidth", 18), KV("wallThickness", 3), KV("openingGap", 5))
            ],
            "simple-box" =>
            [
                Preset("Project box", "Compact utility box with open top.",
                    KV("width", 100), KV("depth", 70), KV("height", 45), KV("wallThickness", 3), KV("openTop", 1), KV("cornerRadius", 2)),
                Preset("Storage bin", "Closed-top printable container shell.",
                    KV("width", 140), KV("depth", 100), KV("height", 70), KV("wallThickness", 3), KV("openTop", 0), KV("cornerRadius", 3))
            ],
            "lid" =>
            [
                Preset("Snap lid", "Shallow lid for a medium electronics box.",
                    KV("width", 100), KV("depth", 70), KV("thickness", 3), KV("lipHeight", 4), KV("tolerance", 0.35)),
                Preset("Large cover", "Broader cover with a taller locating lip.",
                    KV("width", 140), KV("depth", 100), KV("thickness", 3.5), KV("lipHeight", 5), KV("tolerance", 0.45))
            ],
            "gear" =>
            [
                Preset("Small robot gear", "Compact gear for small servo or stepper projects.",
                    KV("teethCount", 16), KV("module", 1.5), KV("thickness", 5), KV("boreDiameter", 5), KV("hubDiameter", 10), KV("hubHeight", 3)),
                Preset("Drive gear", "Larger drive gear for transmission builds.",
                    KV("teethCount", 24), KV("module", 2), KV("thickness", 8), KV("boreDiameter", 8), KV("hubDiameter", 18), KV("hubHeight", 5))
            ],
            "snap-fit-case" =>
            [
                Preset("Small electronics", "Compact snap enclosure for sensor or dev boards.",
                    KV("innerWidth", 60), KV("innerDepth", 45), KV("innerHeight", 25), KV("wallThickness", 2), KV("snapCount", 4), KV("snapOverhang", 1), KV("tolerance", 0.3)),
                Preset("Project box", "Larger snap-together project enclosure.",
                    KV("innerWidth", 100), KV("innerDepth", 75), KV("innerHeight", 35), KV("wallThickness", 2.5), KV("snapCount", 6), KV("snapOverhang", 1.2), KV("tolerance", 0.35))
            ],
            "vaulted-clip" =>
            [
                Preset("Signal cable", "Light grip clip for thin signal wires.",
                    KV("cableDiameter", 4), KV("clipWidth", 12), KV("armLength", 14), KV("wallThickness", 1.8), KV("gripOverlap", 2), KV("mountHoleDia", 3)),
                Preset("Power cable", "Heavy grip clip for thicker cables.",
                    KV("cableDiameter", 10), KV("clipWidth", 20), KV("armLength", 22), KV("wallThickness", 2.5), KV("gripOverlap", 4), KV("mountHoleDia", 4))
            ],
            "heat-set-insert-boss" =>
            [
                Preset("M3 insert", "Standard M3 heat-set insert boss.",
                    KV("insertDiameter", 5.5), KV("insertDepth", 6), KV("wallThickness", 3), KV("bossHeight", 8), KV("baseFillet", 1.5), KV("taperAngle", 2)),
                Preset("M4 insert", "Larger M4 heat-set insert boss.",
                    KV("insertDiameter", 7), KV("insertDepth", 8), KV("wallThickness", 4), KV("bossHeight", 10), KV("baseFillet", 2), KV("taperAngle", 2.5))
            ],
            "gridfinity-bin" =>
            [
                Preset("Small parts bin", "Compact 2x1 bin for screws and small parts.",
                    KV("gridX", 2), KV("gridY", 1), KV("binHeight", 42), KV("wallThickness", 2), KV("magnetHoles", 4), KV("labelTab", 1)),
                Preset("Large storage bin", "Wide 3x2 bin for tools and larger items.",
                    KV("gridX", 3), KV("gridY", 2), KV("binHeight", 63), KV("wallThickness", 2.5), KV("magnetHoles", 6), KV("labelTab", 1))
            ],
            _ => [Preset("Default", "Template default dimensions.")]
        };
    }

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

        return ExpandBuilderSequence(sb.ToString());
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
        var openingGap = Math.Max(V(values, "openingGap"), wallThickness);
        var overall = Math.Max(cableDiameter + wallThickness * 2d, wallThickness * 2d + 2d);
        var depth = Math.Max(clipWidth, wallThickness * 2d + openingGap);
        return $"create box {N(overall)}x{N(depth)}x{N(clipWidth)}; fillet {N(Math.Min(wallThickness, 3d))}";
    }

    private static string BuildDinRailClip(IReadOnlyDictionary<string, double> values)
    {
        var railWidth = V(values, "railWidth");
        var clipHeight = V(values, "clipHeight");
        var wallThickness = V(values, "wallThickness");
        var lipDepth = V(values, "lipDepth");
        var screwHoleDiameter = Math.Max(V(values, "screwHoleDiameter"), 0d);
        var baseDepth = Math.Max(railWidth + wallThickness * 2d, railWidth + lipDepth);
        var sb = new StringBuilder();
        sb.Append($"recipe bracket {N(baseDepth)} {N(wallThickness * 2d + lipDepth)} {N(clipHeight)} {N(wallThickness)}");
        if (screwHoleDiameter > 0.1d)
        {
            sb.Append($"; hole depth {N(Math.Max(wallThickness, 1d))}");
        }

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildTSlotNut(IReadOnlyDictionary<string, double> values)
    {
        var slotWidth = V(values, "slotWidth");
        var nutLength = V(values, "nutLength");
        var nutHeight = V(values, "nutHeight");
        var holeDiameter = Math.Max(V(values, "holeDiameter"), 0d);
        var flangeWidth = Math.Max(V(values, "flangeWidth"), slotWidth + 1d);
        var sb = new StringBuilder();
        sb.Append($"create box {N(flangeWidth)}x{N(nutLength)}x{N(nutHeight)}; chamfer {N(Math.Min(nutHeight / 3d, 1.2d))}");
        if (holeDiameter > 0.1d)
        {
            sb.Append($"; hole depth {N(Math.Max(nutHeight, 1d))}");
        }

        return sb.ToString();
    }

    private static string BuildBoxEnclosure(IReadOnlyDictionary<string, double> values)
    {
        var innerWidth = V(values, "innerWidth");
        var innerHeight = V(values, "innerHeight");
        var innerDepth = V(values, "innerDepth");
        var wallThickness = V(values, "wallThickness");
        var cornerRadius = Math.Max(V(values, "cornerRadius"), 0d);
        var outerWidth = innerWidth + wallThickness * 2d;
        var outerDepth = innerDepth + wallThickness * 2d;
        var outerHeight = innerHeight + wallThickness * 2d;
        var sb = new StringBuilder();
        sb.Append($"recipe shelled-box {N(outerWidth)} {N(outerDepth)} {N(outerHeight)} {N(wallThickness)}");
        if (cornerRadius > 0.05d)
        {
            sb.Append($"; fillet {N(Math.Min(cornerRadius, wallThickness * 1.5d))}");
        }

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildHingeBracket(IReadOnlyDictionary<string, double> values)
    {
        var leafWidth = V(values, "leafWidth");
        var leafLength = V(values, "leafLength");
        var thickness = V(values, "thickness");
        var pinDiameter = V(values, "pinDiameter");
        var knuckleCount = Math.Clamp((int)Math.Round(V(values, "knuckleCount"), MidpointRounding.AwayFromZero), 2, 5);
        var sb = new StringBuilder();
        sb.Append($"recipe bracket {N(leafLength)} {N(leafWidth)} {N(pinDiameter + thickness * 2d)} {N(thickness)}");
        sb.Append($"; circular pattern {knuckleCount} angle 180 around y");
        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildPcbTray(IReadOnlyDictionary<string, double> values)
    {
        var boardWidth = V(values, "boardWidth");
        var boardDepth = V(values, "boardDepth");
        var wallHeight = V(values, "wallHeight");
        var wallThickness = V(values, "wallThickness");
        var standoffHeight = V(values, "standoffHeight");
        var standoffDiameter = V(values, "standoffDiameter");
        var standoffHoleDia = Math.Max(V(values, "standoffHoleDia"), 0d);
        var outerWidth = boardWidth + wallThickness * 2d;
        var outerDepth = boardDepth + wallThickness * 2d;
        var sb = new StringBuilder();
        sb.Append($"recipe shelled-box {N(outerWidth)} {N(outerDepth)} {N(wallHeight)} {N(wallThickness)}");
        sb.Append($"; recipe boss {N(standoffDiameter)} {N(standoffHeight)}");
        if (standoffHoleDia > 0.1d)
        {
            sb.Append($"; hole depth {N(Math.Max(standoffHeight, 1d))}");
        }

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildSimpleBox(IReadOnlyDictionary<string, double> values)
    {
        var width = V(values, "width");
        var depth = V(values, "depth");
        var height = V(values, "height");
        var wallThickness = V(values, "wallThickness");
        var openTop = V(values, "openTop") >= 0.5d;
        var cornerRadius = Math.Max(V(values, "cornerRadius"), 0d);
        var sb = new StringBuilder();
        sb.Append($"recipe shelled-box {N(width)} {N(depth)} {N(height)} {N(wallThickness)}");
        if (!openTop)
        {
            sb.Append($"; create box {N(width)}x{N(depth)}x{N(wallThickness)}");
        }

        if (cornerRadius > 0.05d)
        {
            sb.Append($"; fillet {N(Math.Min(cornerRadius, wallThickness))}");
        }

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildLid(IReadOnlyDictionary<string, double> values)
    {
        var width = V(values, "width");
        var depth = V(values, "depth");
        var thickness = V(values, "thickness");
        var lipHeight = V(values, "lipHeight");
        var tolerance = Math.Max(V(values, "tolerance"), 0d);
        var innerWidth = Math.Max(width - tolerance * 2d, 4d);
        var innerDepth = Math.Max(depth - tolerance * 2d, 4d);
        var sb = new StringBuilder();
        sb.Append($"select plane top; start sketch; rectangle 0 0 {N(width)} {N(depth)}; finish sketch; extrude {N(thickness)}");
        if (lipHeight > 0.05d)
        {
            sb.Append($"; select plane top; start sketch; rectangle {N(tolerance)} {N(tolerance)} {N(innerWidth)} {N(innerDepth)}; finish sketch; extrude {N(lipHeight)}");
        }

        return sb.ToString();
    }

    private static string BuildGear(IReadOnlyDictionary<string, double> values)
    {
        var teeth = ClampCountGear(V(values, "teethCount"));
        var module = Math.Max(V(values, "module"), 0.5d);
        var thickness = V(values, "thickness");
        var boreDiameter = Math.Max(V(values, "boreDiameter"), 0d);
        var hubDiameter = Math.Max(V(values, "hubDiameter"), 0d);
        var hubHeight = Math.Max(V(values, "hubHeight"), 0d);
        var pitchRadius = teeth * module / 2d;
        var outerRadius = pitchRadius + module;

        var sb = new StringBuilder();
        sb.Append($"select plane top; start sketch; circle 0 0 radius {N(outerRadius)}; finish sketch; extrude {N(thickness)}");
        if (boreDiameter > 0.1d)
        {
            sb.Append($"; hole depth {N(Math.Max(thickness + hubHeight, 1d))}");
        }

        if (hubDiameter > 0.1d && hubHeight > 0.05d)
        {
            sb.Append($"; select plane top; start sketch; circle 0 0 radius {N(Math.Max(hubDiameter / 2d, boreDiameter / 2d + 2d))}; finish sketch; extrude {N(hubHeight)}");
        }

        sb.Append($"; fillet {N(Math.Min(module * 0.4d, 2d))}");

        return sb.ToString();
    }

    private static string BuildSnapFitCase(IReadOnlyDictionary<string, double> values)
    {
        var innerWidth = V(values, "innerWidth");
        var innerDepth = V(values, "innerDepth");
        var innerHeight = V(values, "innerHeight");
        var wallThickness = V(values, "wallThickness");
        var snapCount = Math.Clamp((int)Math.Round(V(values, "snapCount"), MidpointRounding.AwayFromZero), 2, 8);
        var snapOverhang = V(values, "snapOverhang");
        var tolerance = Math.Max(V(values, "tolerance"), 0d);
        var outerWidth = innerWidth + wallThickness * 2d;
        var outerDepth = innerDepth + wallThickness * 2d;
        var halfHeight = innerHeight + wallThickness;
        var snapTabWidth = Math.Min(outerWidth / (snapCount + 1d), wallThickness * 4d);

        var sb = new StringBuilder();
        sb.Append($"recipe shelled-box {N(outerWidth)} {N(outerDepth)} {N(halfHeight)} {N(wallThickness)}");
        sb.Append($"; create box {N(outerWidth - tolerance * 2d)}x{N(outerDepth - tolerance * 2d)}x{N(halfHeight)}");
        for (var i = 0; i < snapCount; i++)
        {
            sb.Append($"; create box {N(snapTabWidth)}x{N(snapOverhang)}x{N(wallThickness * 2d)}");
        }

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string BuildVaultedClip(IReadOnlyDictionary<string, double> values)
    {
        var cableDiameter = V(values, "cableDiameter");
        var clipWidth = V(values, "clipWidth");
        var armLength = V(values, "armLength");
        var wallThickness = V(values, "wallThickness");
        var gripOverlap = V(values, "gripOverlap");
        var mountHoleDia = Math.Max(V(values, "mountHoleDia"), 0d);
        var bodyWidth = Math.Max(cableDiameter + wallThickness * 2d + gripOverlap * 2d, wallThickness * 3d);
        var bodyDepth = Math.Max(armLength + wallThickness, wallThickness * 2d);

        var sb = new StringBuilder();
        sb.Append($"select plane top; start sketch; rectangle 0 0 {N(bodyWidth)} {N(bodyDepth)}; finish sketch; extrude {N(clipWidth)}");
        sb.Append($"; select plane front; start sketch; circle {N(bodyWidth / 2d)} {N(bodyDepth / 2d)} radius {N(cableDiameter / 2d)}; finish sketch; extrude cut {N(-clipWidth)}");
        if (mountHoleDia > 0.1d)
        {
            sb.Append($"; hole depth {N(Math.Max(clipWidth, 1d))}");
        }

        sb.Append($"; fillet {N(Math.Min(wallThickness * 1.2d, 3d))}");

        return sb.ToString();
    }

    private static string BuildHeatSetInsertBoss(IReadOnlyDictionary<string, double> values)
    {
        var insertDiameter = V(values, "insertDiameter");
        var insertDepth = V(values, "insertDepth");
        var wallThickness = V(values, "wallThickness");
        var bossHeight = V(values, "bossHeight");
        var baseFillet = V(values, "baseFillet");
        var taperAngle = V(values, "taperAngle");
        var topDiameter = insertDiameter + wallThickness * 2d;
        var taperRadians = taperAngle * Math.PI / 180d;
        var baseDiameter = topDiameter + Math.Tan(taperRadians) * bossHeight * 2d;

        var sb = new StringBuilder();
        sb.Append($"select plane top; start sketch; circle 0 0 radius {N(baseDiameter / 2d)}; finish sketch; extrude {N(bossHeight)}");
        sb.Append($"; hole depth {N(Math.Max(insertDepth, 1d))}");
        if (baseFillet > 0.05d)
        {
            sb.Append($"; fillet {N(baseFillet)}");
        }

        return sb.ToString();
    }

    private static string BuildGridfinityBin(IReadOnlyDictionary<string, double> values)
    {
        var gridX = Math.Clamp((int)Math.Round(V(values, "gridX"), MidpointRounding.AwayFromZero), 1, 6);
        var gridY = Math.Clamp((int)Math.Round(V(values, "gridY"), MidpointRounding.AwayFromZero), 1, 6);
        var binHeight = V(values, "binHeight");
        var wallThickness = V(values, "wallThickness");
        var magnetHoles = Math.Clamp((int)Math.Round(V(values, "magnetHoles"), MidpointRounding.AwayFromZero), 0, 8);
        var labelTab = V(values, "labelTab") >= 0.5d;
        var gridUnit = 42d;
        var outerWidth = gridX * gridUnit;
        var outerDepth = gridY * gridUnit;
        var totalHeight = binHeight + 4.8d;

        var sb = new StringBuilder();
        sb.Append($"recipe shelled-box {N(outerWidth)} {N(outerDepth)} {N(totalHeight)} {N(wallThickness)}");
        sb.Append($"; create box {N(outerWidth - 0.5d)}x{N(outerDepth - 0.5d)}x{N(4.8d)}");
        if (magnetHoles > 0)
        {
            for (var i = 0; i < magnetHoles; i++)
            {
                sb.Append($"; hole depth {N(Math.Max(wallThickness + 1d, 3d))}");
            }
        }

        if (labelTab && outerWidth >= 42d)
        {
            sb.Append($"; create box {N(outerWidth * 0.6d)}x{N(6d)}x{N(10d)}");
        }

        sb.Append($"; fillet {N(Math.Min(wallThickness * 0.6d, 1.5d))}");

        return ExpandBuilderSequence(sb.ToString());
    }

    private static string ExpandBuilderSequence(string text) =>
        CadRecipeLibrary.ExpandSequence(text);

    private static MakerTemplateParameter P(
        string key,
        string displayName,
        double defaultValue,
        double minValue,
        double maxValue,
        string unit,
        string description) =>
        new(key, displayName, defaultValue, minValue, maxValue, unit, description);

    private static MakerTemplatePreset Preset(
        string name,
        string description,
        params KeyValuePair<string, double>[] values) =>
        new(
            name,
            description,
            values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));

    private static KeyValuePair<string, double> KV(string key, double value) => new(key, value);

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

    private static int ClampCountGear(double raw)
    {
        var rounded = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 8, 80);
    }
}
