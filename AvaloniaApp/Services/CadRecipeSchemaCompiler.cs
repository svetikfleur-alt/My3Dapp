using System.Globalization;
using System.Text.Json;

namespace My3DApp.AvaloniaApp.Services;

public static class CadRecipeSchemaCompiler
{
    private const string SchemaName = "my3dapp.cad.recipe.v1";

    public static bool LooksLikeRecipeJson(string input)
    {
        var trimmed = (input ?? string.Empty).TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("```", StringComparison.Ordinal);
    }

    public static bool TryCompileToCommandText(string input, out string commandText, out string? error)
    {
        commandText = string.Empty;
        error = null;

        var jsonText = ExtractJsonObject(input);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "Recipe JSON object was not found.";
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(jsonText);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Recipe root must be a JSON object.";
                return false;
            }

            var recipeRoot = ResolveRecipeRoot(root);
            if (!TryGetProperty(recipeRoot, out var stepsElement, "steps"))
            {
                error = "Recipe JSON must include a steps array.";
                return false;
            }

            if (stepsElement.ValueKind != JsonValueKind.Array)
            {
                error = "Recipe steps must be an array.";
                return false;
            }

            var commands = new List<string>();
            var index = 1;
            foreach (var step in stepsElement.EnumerateArray())
            {
                var stepCommands = CompileStep(step, index);
                commands.AddRange(stepCommands);
                index++;
            }

            if (commands.Count == 0)
            {
                error = "Recipe did not produce any executable CAD commands.";
                return false;
            }

            commandText = string.Join("; ", commands);
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Recipe JSON is invalid: {ex.Message}";
            return false;
        }
        catch (CadRecipeSchemaException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string BuildSampleBoxJson(double width, double depth, double height)
    {
        return $$"""
        {
          "schema": "{{SchemaName}}",
          "id": "sample-box",
          "title": "Sample Box",
          "units": "mm",
          "description": "Creates one visible extruded box body in the active part studio.",
          "steps": [
            {
              "id": "create-body",
              "operation": "create_box",
              "parameters": {
                "width": {{N(width)}},
                "depth": {{N(depth)}},
                "height": {{N(height)}}
              }
            }
          ]
        }
        """;
    }

    private static JsonElement ResolveRecipeRoot(JsonElement root)
    {
        if (TryGetProperty(root, out var recipe, "recipe") && recipe.ValueKind == JsonValueKind.Object)
        {
            return recipe;
        }

        return root;
    }

    private static IReadOnlyList<string> CompileStep(JsonElement step, int index)
    {
        if (step.ValueKind == JsonValueKind.String)
        {
            var command = step.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                throw StepError(index, "string", "Recipe command step is empty.");
            }

            return [command];
        }

        if (step.ValueKind != JsonValueKind.Object)
        {
            throw StepError(index, "unknown", "Recipe step must be an object or command string.");
        }

        var commandText = GetString(step, "command", "script", "text");
        if (!string.IsNullOrWhiteSpace(commandText))
        {
            return [commandText.Trim()];
        }

        var operation = GetString(step, "operation", "op", "action", "type");
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw StepError(index, "unknown", "Recipe step is missing an operation.");
        }

        return NormalizeOperation(operation) switch
        {
            "create_box" => CompileCreateBox(step, index, operation),
            "box" => CompileCreateBox(step, index, operation),
            "create_cylinder" => CompileCreateCylinder(step, index, operation),
            "cylinder" => CompileCreateCylinder(step, index, operation),
            "create_sphere" => CompileCreateSphere(step, index, operation),
            "sphere" => CompileCreateSphere(step, index, operation),
            "subtract_hole" => CompileSubtractHole(step, index, operation),
            "hole" => CompileSubtractHole(step, index, operation),
            "cut_hole" => CompileSubtractHole(step, index, operation),
            "fillet" => CompileFillet(step, index, operation),
            "chamfer" => CompileChamfer(step, index, operation),
            "move" => CompileMove(step, index, operation),
            "translate" => CompileMove(step, index, operation),
            "rotate" => throw StepError(index, operation, "Rotate is not supported by the current body command executor. No CAD kernel rotation feature is available yet."),
            _ => throw StepError(index, operation, $"Unsupported recipe operation '{operation}'.")
        };
    }

    private static IReadOnlyList<string> CompileCreateBox(JsonElement step, int index, string operation)
    {
        var width = ReadPositive(step, index, operation, "width", "w", "length", "l", "x");
        var depth = ReadPositive(step, index, operation, "depth", "d", "y");
        var height = ReadPositive(step, index, operation, "height", "h", "z");
        return [$"create box {N(width)}x{N(depth)}x{N(height)}"];
    }

    private static IReadOnlyList<string> CompileCreateCylinder(JsonElement step, int index, string operation)
    {
        var radius = TryReadPositive(step, out var rawRadius, "radius", "r")
            ? rawRadius
            : TryReadPositive(step, out var diameter, "diameter", "dia")
                ? diameter / 2d
                : throw StepError(index, operation, "create_cylinder requires radius or diameter.");
        var height = ReadPositive(step, index, operation, "height", "h", "depth");
        return [$"create cylinder radius {N(radius)} height {N(height)}"];
    }

    private static IReadOnlyList<string> CompileCreateSphere(JsonElement step, int index, string operation)
    {
        if (TryReadPositive(step, out _, "radius", "r", "diameter", "dia"))
        {
            throw StepError(index, operation, "Parameterized create_sphere is not supported by the current command executor. Use create_sphere without a radius to create the default sphere.");
        }

        return ["create sphere"];
    }

    private static IReadOnlyList<string> CompileSubtractHole(JsonElement step, int index, string operation)
    {
        var diameter = TryReadPositive(step, out var rawDiameter, "diameter", "dia", "d")
            ? rawDiameter
            : TryReadPositive(step, out var radius, "radius", "r")
                ? radius * 2d
                : throw StepError(index, operation, "subtract_hole requires diameter or radius.");

        var x = TryReadNumber(step, out var centerX, "x", "offsetX", "offset_x", "centerX", "center_x") ? centerX : 0d;
        var y = TryReadNumber(step, out var centerY, "y", "offsetY", "offset_y", "centerY", "center_y") ? centerY : 0d;
        var depthPart = TryReadPositive(step, out var depth, "depth", "depthValue", "depth_value")
            ? $" depth {N(depth)}"
            : " depth through";

        return [$"hole diameter {N(diameter)}{depthPart} offset {N(x)} {N(y)}"];
    }

    private static IReadOnlyList<string> CompileFillet(JsonElement step, int index, string operation)
    {
        var radius = ReadPositive(step, index, operation, "radius", "r");
        return [$"fillet {N(radius)}"];
    }

    private static IReadOnlyList<string> CompileChamfer(JsonElement step, int index, string operation)
    {
        var distance = ReadPositive(step, index, operation, "distance", "d", "width");
        return [$"chamfer {N(distance)}"];
    }

    private static IReadOnlyList<string> CompileMove(JsonElement step, int index, string operation)
    {
        var commands = new List<string>();
        var axis = GetString(step, "axis")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(axis))
        {
            if (axis is not ("x" or "y" or "z"))
            {
                throw StepError(index, operation, "Move axis must be x, y, or z.");
            }

            var distance = ReadNumber(step, index, operation, "distance", "amount", "value");
            if (Math.Abs(distance) <= 0.000001d)
            {
                throw StepError(index, operation, "Move distance must be non-zero.");
            }

            return [$"move body {N(distance)} in {axis}"];
        }

        AddMoveCommand(commands, "x", TryReadNumber(step, out var x, "x", "dx") ? x : 0d);
        AddMoveCommand(commands, "y", TryReadNumber(step, out var y, "y", "dy") ? y : 0d);
        AddMoveCommand(commands, "z", TryReadNumber(step, out var z, "z", "dz") ? z : 0d);

        if (commands.Count == 0)
        {
            throw StepError(index, operation, "Move requires a non-zero x, y, z, or axis/distance parameter.");
        }

        return commands;
    }

    private static void AddMoveCommand(ICollection<string> commands, string axis, double distance)
    {
        if (Math.Abs(distance) > 0.000001d)
        {
            commands.Add($"move body {N(distance)} in {axis}");
        }
    }

    private static double ReadPositive(JsonElement step, int index, string operation, params string[] names)
    {
        if (!TryReadPositive(step, out var value, names))
        {
            throw StepError(index, operation, $"Missing or invalid positive parameter: {string.Join("/", names)}.");
        }

        return value;
    }

    private static double ReadNumber(JsonElement step, int index, string operation, params string[] names)
    {
        if (!TryReadNumber(step, out var value, names))
        {
            throw StepError(index, operation, $"Missing or invalid numeric parameter: {string.Join("/", names)}.");
        }

        return value;
    }

    private static bool TryReadPositive(JsonElement step, out double value, params string[] names)
    {
        if (TryReadNumber(step, out value, names) && value > 0d && double.IsFinite(value))
        {
            return true;
        }

        value = 0d;
        return false;
    }

    private static bool TryReadNumber(JsonElement step, out double value, params string[] names)
    {
        value = 0d;
        if (!TryGetParameter(step, out var element, names))
        {
            return false;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDouble(out value) && double.IsFinite(value);
            case JsonValueKind.String:
                return double.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value) && double.IsFinite(value);
            default:
                return false;
        }
    }

    private static bool TryGetParameter(JsonElement step, out JsonElement value, params string[] names)
    {
        if (TryGetProperty(step, out var parameters, "parameters", "params", "args") &&
            parameters.ValueKind == JsonValueKind.Object &&
            TryGetProperty(parameters, out value, names))
        {
            return true;
        }

        return TryGetProperty(step, out value, names);
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        if (!TryGetProperty(element, out var property, names))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        foreach (var item in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string ExtractJsonObject(string input)
    {
        var trimmed = (input ?? string.Empty).Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var fenceStart = trimmed.IndexOf('{');
            var fenceEnd = trimmed.LastIndexOf('}');
            return fenceStart >= 0 && fenceEnd > fenceStart
                ? trimmed[fenceStart..(fenceEnd + 1)]
                : string.Empty;
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : string.Empty;
    }

    private static string NormalizeOperation(string operation)
    {
        return operation.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static CadRecipeSchemaException StepError(int index, string operation, string message) =>
        new($"Recipe step {index} ({operation}): {message}");

    private sealed class CadRecipeSchemaException(string message) : Exception(message);
}
