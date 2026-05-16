using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace My3DApp.AvaloniaApp.Services;

public sealed record CadRecipe(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Parameters,
    string Example,
    Func<double[], string> Build)
{
    public string Signature => $"recipe {Name} {string.Join(' ', Parameters.Select(p => p.ToLowerInvariant()))}";
}

public static class CadRecipeLibrary
{
    private static readonly Regex RecipeInvocationPattern = new(
        @"^recipe\s+(?<name>[a-z][a-z0-9\-]*)(?<args>(?:\s+[+-]?\d+(?:\.\d+)?)*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<CadRecipe> All { get; } = BuildCatalog();

    public static IReadOnlyList<string> Categories { get; } = ["Plates & blocks", "Shafts & disks", "Brackets", "Profiles"];

    public static CadRecipe? Find(string name)
    {
        var key = name.Trim().ToLowerInvariant();
        return All.FirstOrDefault(r => string.Equals(r.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    public static string ExpandSequence(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var parts = Regex.Split(input, @"(?:\r?\n|;|->|\bthen\b)", RegexOptions.IgnoreCase);
        var result = new StringBuilder();
        var first = true;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (!first)
            {
                result.Append("; ");
            }

            if (TryExpandLine(trimmed, out var expanded))
            {
                result.Append(expanded);
            }
            else
            {
                result.Append(trimmed);
            }

            first = false;
        }

        return result.ToString();
    }

    public static bool TryExpandLine(string line, out string expanded)
    {
        expanded = string.Empty;
        var match = RecipeInvocationPattern.Match(line.Trim());
        if (!match.Success)
        {
            return false;
        }

        var recipe = Find(match.Groups["name"].Value);
        if (recipe is null)
        {
            return false;
        }

        var args = ParseArgs(match.Groups["args"].Value, recipe.Parameters.Count);
        try
        {
            expanded = recipe.Build(args);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double[] ParseArgs(string raw, int expectedCount)
    {
        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new double[Math.Max(expectedCount, tokens.Length)];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                parsed[i] = value;
            }
        }
        return parsed;
    }

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static IReadOnlyList<CadRecipe> BuildCatalog()
    {
        return new CadRecipe[]
        {
            new("plate",
                "Plate",
                "Rectangular plate L × W × T (length × width × thickness).",
                ["length", "width", "thickness"],
                "recipe plate 40 30 5",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 40;
                    var w = a[1] > 0 ? a[1] : 30;
                    var t = a[2] > 0 ? a[2] : 5;
                    return $"select plane top; start sketch; rectangle 0 0 {N(l)} {N(w)}; finish sketch; extrude {N(t)}";
                }),

            new("shaft",
                "Shaft",
                "Cylindrical shaft of radius R and height H.",
                ["radius", "height"],
                "recipe shaft 5 30",
                a =>
                {
                    var r = a[0] > 0 ? a[0] : 5;
                    var h = a[1] > 0 ? a[1] : 30;
                    return $"select plane top; start sketch; circle 0 0 radius {N(r)}; finish sketch; extrude {N(h)}";
                }),

            new("disk",
                "Disk",
                "Thin disk of radius R and thickness T.",
                ["radius", "thickness"],
                "recipe disk 20 3",
                a =>
                {
                    var r = a[0] > 0 ? a[0] : 20;
                    var t = a[1] > 0 ? a[1] : 3;
                    return $"select plane top; start sketch; circle 0 0 radius {N(r)}; finish sketch; extrude {N(t)}";
                }),

            new("boss",
                "Boss",
                "Cylindrical boss with diameter D and height H.",
                ["diameter", "height"],
                "recipe boss 8 12",
                a =>
                {
                    var d = a[0] > 0 ? a[0] : 8;
                    var h = a[1] > 0 ? a[1] : 12;
                    return $"select plane top; start sketch; circle 0 0 radius {N(d / 2)}; finish sketch; extrude {N(h)}";
                }),

            new("bracket",
                "L-bracket",
                "L-shaped bracket: base plate L × W × T with a vertical wall of height H.",
                ["length", "width", "height", "thickness"],
                "recipe bracket 40 30 25 4",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 40;
                    var w = a[1] > 0 ? a[1] : 30;
                    var h = a[2] > 0 ? a[2] : 25;
                    var t = a[3] > 0 ? a[3] : 4;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; rectangle 0 0 {N(l)} {N(w)}; finish sketch; extrude {N(t)}");
                    sb.Append($"; select plane front; start sketch; rectangle 0 0 {N(l)} {N(h)}; finish sketch; extrude {N(t)}");
                    return sb.ToString();
                }),

            new("filleted-box",
                "Filleted box",
                "Box L × W × H with all edges filleted to radius R.",
                ["length", "width", "height", "radius"],
                "recipe filleted-box 40 30 20 3",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 40;
                    var w = a[1] > 0 ? a[1] : 30;
                    var h = a[2] > 0 ? a[2] : 20;
                    var r = a[3] > 0 ? a[3] : 3;
                    return $"create box {N(l)}x{N(w)}x{N(h)}; fillet {N(r)}";
                }),

            new("shelled-box",
                "Enclosure",
                "Hollow box L × W × H with wall thickness T.",
                ["length", "width", "height", "thickness"],
                "recipe shelled-box 60 40 30 2",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 60;
                    var w = a[1] > 0 ? a[1] : 40;
                    var h = a[2] > 0 ? a[2] : 30;
                    var t = a[3] > 0 ? a[3] : 2;
                    return $"create box {N(l)}x{N(w)}x{N(h)}; shell {N(t)}";
                }),

            new("rounded-plate",
                "Rounded plate",
                "Plate L × W × T with corners filleted to radius R.",
                ["length", "width", "thickness", "radius"],
                "recipe rounded-plate 50 30 4 5",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 50;
                    var w = a[1] > 0 ? a[1] : 30;
                    var t = a[2] > 0 ? a[2] : 4;
                    var r = a[3] > 0 ? a[3] : 5;
                    return $"select plane top; start sketch; rectangle 0 0 {N(l)} {N(w)}; finish sketch; extrude {N(t)}; fillet {N(r)}";
                }),

            new("stepped-shaft",
                "Stepped shaft",
                "Two-diameter shaft: section 1 (R1, H1) stacked under section 2 (R2, H2).",
                ["radius1", "height1", "radius2", "height2"],
                "recipe stepped-shaft 10 20 6 25",
                a =>
                {
                    var r1 = a[0] > 0 ? a[0] : 10;
                    var h1 = a[1] > 0 ? a[1] : 20;
                    var r2 = a[2] > 0 ? a[2] : 6;
                    var h2 = a[3] > 0 ? a[3] : 25;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(r1)}; finish sketch; extrude {N(h1)}");
                    sb.Append($"; select plane top; start sketch; circle 0 0 radius {N(r2)}; finish sketch; extrude {N(h2)}");
                    return sb.ToString();
                }),

            new("flange",
                "Flange",
                "Flanged shaft: wide flange (Rf × Tf) with a centered shaft (Rs × Hs) on top.",
                ["flange-radius", "flange-thickness", "shaft-radius", "shaft-height"],
                "recipe flange 18 4 6 25",
                a =>
                {
                    var rf = a[0] > 0 ? a[0] : 18;
                    var tf = a[1] > 0 ? a[1] : 4;
                    var rs = a[2] > 0 ? a[2] : 6;
                    var hs = a[3] > 0 ? a[3] : 25;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(rf)}; finish sketch; extrude {N(tf)}");
                    sb.Append($"; select plane top; start sketch; circle 0 0 radius {N(rs)}; finish sketch; extrude {N(hs)}");
                    return sb.ToString();
                }),

            new("tower",
                "Rounded tower",
                "Tall rectangular tower L × W × H with filleted edges (R).",
                ["length", "width", "height", "radius"],
                "recipe tower 30 30 80 4",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 30;
                    var w = a[1] > 0 ? a[1] : 30;
                    var h = a[2] > 0 ? a[2] : 80;
                    var r = a[3] > 0 ? a[3] : 4;
                    return $"create box {N(l)}x{N(w)}x{N(h)}; fillet {N(r)}";
                }),

            new("housing",
                "Housing",
                "Filleted, shelled enclosure: box L × W × H, fillet R, then shell T.",
                ["length", "width", "height", "radius", "thickness"],
                "recipe housing 60 40 25 4 2",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 60;
                    var w = a[1] > 0 ? a[1] : 40;
                    var h = a[2] > 0 ? a[2] : 25;
                    var r = a[3] > 0 ? a[3] : 4;
                    var t = a[4] > 0 ? a[4] : 2;
                    return $"create box {N(l)}x{N(w)}x{N(h)}; fillet {N(r)}; shell {N(t)}";
                }),

            new("hollow-cylinder",
                "Hollow cylinder",
                "Tube: outer radius Ro, inner radius Ri, height H.",
                ["outer-radius", "inner-radius", "height"],
                "recipe hollow-cylinder 12 9 25",
                a =>
                {
                    var ro = a[0] > 0 ? a[0] : 12;
                    var ri = a[1] > 0 && a[1] < a[0] ? a[1] : Math.Max(0.5, ro - 3);
                    var h = a[2] > 0 ? a[2] : 25;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(ro)}; finish sketch; extrude {N(h)}");
                    sb.Append($"; select plane top; start sketch; circle 0 0 radius {N(ri)}; finish sketch; extrude cut {N(h)}");
                    return sb.ToString();
                }),

            new("plate-with-hole",
                "Plate with hole",
                "Plate L × W × T with a centered through-hole of radius R.",
                ["length", "width", "thickness", "hole-radius"],
                "recipe plate-with-hole 50 30 5 4",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 50;
                    var w = a[1] > 0 ? a[1] : 30;
                    var t = a[2] > 0 ? a[2] : 5;
                    var r = a[3] > 0 ? a[3] : 4;
                    var cx = l / 2;
                    var cy = w / 2;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; rectangle 0 0 {N(l)} {N(w)}; finish sketch; extrude {N(t)}");
                    sb.Append($"; select plane top; start sketch; circle {N(cx)} {N(cy)} radius {N(r)}; finish sketch; extrude cut {N(t)}");
                    return sb.ToString();
                }),

            new("filleted-shaft",
                "Filleted shaft",
                "Cylindrical shaft (R, H) with the top edge filleted to Rf.",
                ["radius", "height", "fillet-radius"],
                "recipe filleted-shaft 8 30 1.5",
                a =>
                {
                    var r = a[0] > 0 ? a[0] : 8;
                    var h = a[1] > 0 ? a[1] : 30;
                    var rf = a[2] > 0 ? a[2] : 1.5;
                    return $"select plane top; start sketch; circle 0 0 radius {N(r)}; finish sketch; extrude {N(h)}; fillet {N(rf)}";
                }),

            new("standoff",
                "Standoff",
                "Hollow standoff: outer Ro, inner Ri (clearance hole), height H.",
                ["outer-radius", "inner-radius", "height"],
                "recipe standoff 5 1.6 20",
                a =>
                {
                    var ro = a[0] > 0 ? a[0] : 5;
                    var ri = a[1] > 0 && a[1] < a[0] ? a[1] : Math.Max(0.5, ro - 2);
                    var h = a[2] > 0 ? a[2] : 20;
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; circle 0 0 radius {N(ro)}; finish sketch; extrude {N(h)}");
                    sb.Append($"; select plane top; start sketch; circle 0 0 radius {N(ri)}; finish sketch; extrude cut {N(h)}");
                    return sb.ToString();
                }),

            new("channel",
                "Channel",
                "Rectangular trough: outer L × W × H with a centered cavity (T-wall thickness).",
                ["length", "width", "height", "wall"],
                "recipe channel 60 30 20 3",
                a =>
                {
                    var l = a[0] > 0 ? a[0] : 60;
                    var w = a[1] > 0 ? a[1] : 30;
                    var h = a[2] > 0 ? a[2] : 20;
                    var t = a[3] > 0 ? a[3] : 3;
                    var ix = t;
                    var iy = t;
                    var iw = Math.Max(1, l - 2 * t);
                    var ih = Math.Max(1, w - 2 * t);
                    var sb = new StringBuilder();
                    sb.Append($"select plane top; start sketch; rectangle 0 0 {N(l)} {N(w)}; finish sketch; extrude {N(h)}");
                    sb.Append($"; select plane top; start sketch; rectangle {N(ix)} {N(iy)} {N(ix + iw)} {N(iy + ih)}; finish sketch; extrude cut {N(h - t)}");
                    return sb.ToString();
                }),
        };
    }
}
