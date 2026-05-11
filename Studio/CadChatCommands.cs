using System.Globalization;
using System.Text.RegularExpressions;

namespace My3DApp.Studio;

public sealed class CadChatCommandProcessor
{
    public CadChatResult Execute(CadDocument document, string input, CadBody? selectedBody)
    {
        var text = input.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return CadChatResult.Error("Command is empty.");
        }

        document.Scene.EnsureReferencePlanes();

        var createBox = Regex.Match(
            text,
            @"^(create|add)\s+(box|cube)\s+([+-]?\d+(?:\.\d+)?)x([+-]?\d+(?:\.\d+)?)x([+-]?\d+(?:\.\d+)?)$",
            RegexOptions.IgnoreCase);
        if (createBox.Success)
        {
            var index = NextIndex(document, "Cube");
            var body = new CadBody
            {
                Name = $"Cube{index}"
            };

            body.Features.Add(new BoxFeature
            {
                Name = $"Cube{index}",
                Width = ParseDouble(createBox.Groups[3].Value),
                Depth = ParseDouble(createBox.Groups[4].Value),
                Height = ParseDouble(createBox.Groups[5].Value)
            });

            document.Scene.Bodies.Add(body);
            return CadChatResult.Success(
                $"Created cube {createBox.Groups[3].Value} x {createBox.Groups[4].Value} x {createBox.Groups[5].Value}.",
                body);
        }

        var createCylinder = Regex.Match(
            text,
            @"^(add|create)\s+cylinder\s+radius\s+([+-]?\d+(?:\.\d+)?)\s+height\s+([+-]?\d+(?:\.\d+)?)$",
            RegexOptions.IgnoreCase);
        if (createCylinder.Success)
        {
            var index = NextIndex(document, "Cylinder");
            var body = new CadBody
            {
                Name = $"Cylinder{index}"
            };

            body.Features.Add(new CylinderFeature
            {
                Name = $"Cylinder{index}",
                Radius = ParseDouble(createCylinder.Groups[2].Value),
                Height = ParseDouble(createCylinder.Groups[3].Value)
            });

            document.Scene.Bodies.Add(body);
            return CadChatResult.Success(
                $"Added cylinder radius {createCylinder.Groups[2].Value}, height {createCylinder.Groups[3].Value}.",
                body);
        }

        var createSphere = Regex.Match(
            text,
            @"^(add|create)\s+sphere(?:\s+radius)?\s+([+-]?\d+(?:\.\d+)?)$",
            RegexOptions.IgnoreCase);
        if (createSphere.Success)
        {
            var index = NextIndex(document, "Sphere");
            var body = new CadBody
            {
                Name = $"Sphere{index}"
            };

            body.Features.Add(new SphereFeature
            {
                Name = $"Sphere{index}",
                Radius = ParseDouble(createSphere.Groups[2].Value)
            });

            document.Scene.Bodies.Add(body);
            return CadChatResult.Success(
                $"Added sphere radius {createSphere.Groups[2].Value}.",
                body);
        }

        var move = Regex.Match(
            text,
            @"^move\s+(object|body|[A-Za-z_][A-Za-z0-9_]*)\s+([+-]?\d+(?:\.\d+)?)\s+in\s+([xyz])$",
            RegexOptions.IgnoreCase);
        if (move.Success)
        {
            var body = ResolveBody(document, selectedBody, move.Groups[1].Value);
            if (body is null)
            {
                return CadChatResult.Error("Could not resolve which body to move. Select a body or use its name.");
            }

            var delta = ParseDouble(move.Groups[2].Value);
            var axis = move.Groups[3].Value.ToUpperInvariant();
            var moveFeature = new MoveFeature
            {
                Name = $"Move{body.Features.Count(feature => feature is MoveFeature) + 1}"
            };

            switch (axis)
            {
                case "X":
                    moveFeature.X = delta;
                    break;
                case "Y":
                    moveFeature.Y = delta;
                    break;
                case "Z":
                    moveFeature.Z = delta;
                    break;
            }

            body.Features.Add(moveFeature);
            return CadChatResult.Success($"Added move feature to {body.Name}: {axis} {delta:0.##}.", body, moveFeature);
        }

        return CadChatResult.Error(
            "Command not recognized. Try: 'create cube 20x10x5', 'add cylinder radius 5 height 20', 'add sphere radius 8', or 'move object +10 in x'.");
    }

    private static int NextIndex(CadDocument document, string prefix)
    {
        var max = 0;
        foreach (var body in document.Scene.Bodies)
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

    private static CadBody? ResolveBody(CadDocument document, CadBody? selectedBody, string target)
    {
        if (target.Equals("object", StringComparison.OrdinalIgnoreCase) ||
            target.Equals("body", StringComparison.OrdinalIgnoreCase))
        {
            return selectedBody ?? document.Scene.Bodies.LastOrDefault();
        }

        return document.Scene.Bodies.FirstOrDefault(
            body => body.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
    }

    private static double ParseDouble(string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }
}

public sealed class CadChatResult
{
    public bool IsSuccess { get; private init; }

    public string Message { get; private init; } = string.Empty;

    public CadBody? SelectedBody { get; private init; }

    public CadFeature? SelectedFeature { get; private init; }

    public static CadChatResult Success(string message, CadBody? selectedBody = null, CadFeature? selectedFeature = null) =>
        new()
        {
            IsSuccess = true,
            Message = message,
            SelectedBody = selectedBody,
            SelectedFeature = selectedFeature
        };

    public static CadChatResult Error(string message) =>
        new()
        {
            IsSuccess = false,
            Message = message
        };
}
