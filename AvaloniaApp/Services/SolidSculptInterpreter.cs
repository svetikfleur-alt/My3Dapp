using System.Text;
using FormaCore.Core;
using FormaCore.Engine;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>Result of running an SolidSculpt script.</summary>
public sealed class SolidSculptResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>The final sculpted mesh, or null on error.</summary>
    public Mesh? Mesh { get; init; }
    public int LinesExecuted { get; init; }
    public string Log { get; init; } = string.Empty;

    public static SolidSculptResult Success(Mesh mesh, int lines, string log) =>
        new() { IsSuccess = true, Mesh = mesh, LinesExecuted = lines, Log = log };
    public static SolidSculptResult Error(string msg) =>
        new() { IsSuccess = false, ErrorMessage = msg };
}

/// <summary>
/// SolidSculpt DSL interpreter.
/// Syntax:
///   base sphere [radius]           — start from a sphere (default r=30)
///   base box [w] [h] [d]           — start from a box
///   base cylinder [r] [h]          — start from a cylinder
///   subdivide [n]                  — loop subdivide n times (default 1)
///   smooth [iterations] [strength] — Laplacian smooth
///   inflate [amount]               — push along normals
///   noise [scale] [strength] [seed]— procedural noise
///   pinch [strength] [falloff]     — pull toward centroid
///   twist [degrees]                — twist around Z axis
///   bend [degrees]                 — bend along X axis
///   taper [topScale] [botScale]    — scale XY by height fraction
///   spherize [strength] [radius]   — blend toward a sphere
///   var name = expr                — variable definition
///   repeat N ... end               — loop N times
///   // comment                     — ignored
/// </summary>
public sealed class SolidSculptInterpreter
{
    private const int MaxRepeat = 100;

    private readonly Dictionary<string, double> _vars = new(StringComparer.OrdinalIgnoreCase);
    private Mesh? _mesh;
    private readonly StringBuilder _log = new();
    private int _linesExecuted;

    public SolidSculptResult Execute(string script)
    {
        _vars.Clear();
        _mesh = null;
        _log.Clear();
        _linesExecuted = 0;

        var lines = script
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//"))
            .ToList();

        var error = RunBlock(lines, 0, out _);
        if (error is not null)
            return SolidSculptResult.Error(error);

        if (_mesh is null)
            return SolidSculptResult.Error("No base shape defined. Use 'base sphere', 'base box', or 'base cylinder' first.");

        return SolidSculptResult.Success(_mesh, _linesExecuted, _log.ToString());
    }

    private string? RunBlock(List<string> lines, int startIndex, out int nextIndex)
    {
        nextIndex = startIndex;
        while (nextIndex < lines.Count)
        {
            var line = lines[nextIndex];
            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                nextIndex++;
                return null; // end of block
            }

            _linesExecuted++;
            var error = ExecuteLine(lines, ref nextIndex);
            if (error is not null) return error;
        }
        return null;
    }

    private string? ExecuteLine(List<string> lines, ref int index)
    {
        var line = lines[index++];
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return null;

        var cmd = tokens[0].ToLowerInvariant();

        // Variable assignment
        if (cmd == "var" && tokens.Length >= 4 && tokens[2] == "=")
        {
            var expr = string.Join(" ", tokens.Skip(3));
            var (val, err) = Eval(expr);
            if (err is not null) return err;
            _vars[tokens[1]] = val;
            _log.AppendLine($"var {tokens[1]} = {val}");
            return null;
        }

        if (tokens.Length >= 3 && tokens[1] == "=")
        {
            var expr = string.Join(" ", tokens.Skip(2));
            var (val, err) = Eval(expr);
            if (err is not null) return err;
            _vars[tokens[0]] = val;
            return null;
        }

        // Repeat loop
        if (cmd == "repeat")
        {
            if (tokens.Length < 2) return "repeat requires a count.";
            var (count, err) = Eval(tokens[1]);
            if (err is not null) return err;
            int n = (int)Math.Round(count);
            if (n < 0 || n > MaxRepeat) return $"repeat count must be 0–{MaxRepeat}.";

            // collect body lines until 'end'
            var body = new List<string>();
            while (index < lines.Count && !lines[index].Equals("end", StringComparison.OrdinalIgnoreCase))
                body.Add(lines[index++]);
            if (index < lines.Count) index++; // consume 'end'

            for (var i = 0; i < n; i++)
            {
                int dummy = 0;
                var loopErr = RunBlock(body, 0, out _);
                if (loopErr is not null) return loopErr;
            }
            return null;
        }

        return ExecuteOp(cmd, tokens);
    }

    private string? ExecuteOp(string cmd, string[] tokens)
    {
        switch (cmd)
        {
            case "base":
                return HandleBase(tokens);
            case "subdivide":
                return HandleSubdivide(tokens);
            case "smooth":
                return HandleSmooth(tokens);
            case "inflate":
                return HandleInflate(tokens);
            case "noise":
                return HandleNoise(tokens);
            case "pinch":
                return HandlePinch(tokens);
            case "twist":
                return HandleTwist(tokens);
            case "bend":
                return HandleBend(tokens);
            case "taper":
                return HandleTaper(tokens);
            case "spherize":
                return HandleSpherize(tokens);
            default:
                _log.AppendLine($"Warning: unknown command '{cmd}' — skipped.");
                return null;
        }
    }

    private string? HandleBase(string[] tokens)
    {
        if (tokens.Length < 2) return "base requires a shape type: sphere, box, or cylinder.";
        var shape = tokens[1].ToLowerInvariant();
        switch (shape)
        {
            case "sphere":
            {
                var r = GetArg(tokens, 2, 30);
                _mesh = MeshBuilder.CreateSphere(r);
                _log.AppendLine($"base sphere r={r}  ({_mesh.Vertices.Count} verts)");
                return null;
            }
            case "box":
            {
                var w = GetArg(tokens, 2, 40);
                var h = GetArg(tokens, 3, 40);
                var d = GetArg(tokens, 4, 40);
                _mesh = MeshBuilder.CreateBox(w, h, d);
                _log.AppendLine($"base box {w}×{h}×{d}  ({_mesh.Vertices.Count} verts)");
                return null;
            }
            case "cylinder":
            {
                var r = GetArg(tokens, 2, 20);
                var h = GetArg(tokens, 3, 60);
                _mesh = MeshBuilder.CreateCylinder(r, h);
                _log.AppendLine($"base cylinder r={r} h={h}  ({_mesh.Vertices.Count} verts)");
                return null;
            }
            default:
                return $"Unknown base shape '{shape}'. Use sphere, box, or cylinder.";
        }
    }

    private string? HandleSubdivide(string[] tokens)
    {
        if (_mesh is null) return "No base shape. Define a base before sculpting.";
        int n = (int)Math.Round(GetArg(tokens, 1, 1));
        if (n < 1 || n > 5) return "subdivide count must be 1–5 (each step quadruples triangles).";
        _mesh = SolidSculptEngine.Subdivide(_mesh, n);
        _log.AppendLine($"subdivide ×{n}  → {_mesh.Triangles.Count} tris");
        return null;
    }

    private string? HandleSmooth(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        int iters = (int)Math.Round(GetArg(tokens, 1, 3));
        double strength = GetArg(tokens, 2, 0.5);
        _mesh = SolidSculptEngine.Smooth(_mesh, iters, strength);
        _log.AppendLine($"smooth iters={iters} strength={strength}");
        return null;
    }

    private string? HandleInflate(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double amount = GetArg(tokens, 1, 5.0);
        _mesh = SolidSculptEngine.Inflate(_mesh, amount);
        _log.AppendLine($"inflate {amount}");
        return null;
    }

    private string? HandleNoise(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double scale = GetArg(tokens, 1, 0.1);
        double strength = GetArg(tokens, 2, 5.0);
        int seed = (int)Math.Round(GetArg(tokens, 3, 42));
        _mesh = SolidSculptEngine.Noise(_mesh, scale, strength, seed);
        _log.AppendLine($"noise scale={scale} strength={strength} seed={seed}");
        return null;
    }

    private string? HandlePinch(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double strength = GetArg(tokens, 1, 0.3);
        double falloff = GetArg(tokens, 2, 50.0);
        _mesh = SolidSculptEngine.Pinch(_mesh, strength, falloff);
        _log.AppendLine($"pinch strength={strength} falloff={falloff}");
        return null;
    }

    private string? HandleTwist(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double degrees = GetArg(tokens, 1, 45.0);
        _mesh = SolidSculptEngine.Twist(_mesh, degrees);
        _log.AppendLine($"twist {degrees}°");
        return null;
    }

    private string? HandleBend(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double degrees = GetArg(tokens, 1, 30.0);
        _mesh = SolidSculptEngine.Bend(_mesh, degrees);
        _log.AppendLine($"bend {degrees}°");
        return null;
    }

    private string? HandleTaper(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double top = GetArg(tokens, 1, 0.5);
        double bot = GetArg(tokens, 2, 1.0);
        _mesh = SolidSculptEngine.Taper(_mesh, top, bot);
        _log.AppendLine($"taper top={top} bot={bot}");
        return null;
    }

    private string? HandleSpherize(string[] tokens)
    {
        if (_mesh is null) return "No base shape.";
        double strength = GetArg(tokens, 1, 0.5);
        double radius = GetArg(tokens, 2, -1);
        _mesh = SolidSculptEngine.Spherize(_mesh, strength, radius);
        _log.AppendLine($"spherize strength={strength}");
        return null;
    }

    private double GetArg(string[] tokens, int index, double defaultValue)
    {
        if (index >= tokens.Length) return defaultValue;
        var (val, _) = Eval(tokens[index]);
        return val;
    }

    private (double Value, string? Error) Eval(string expr)
    {
        try
        {
            var substituted = SubstituteVars(expr);
            return (EvalExpr(substituted.Trim()), null);
        }
        catch (Exception ex)
        {
            return (0, $"Expression error in '{expr}': {ex.Message}");
        }
    }

    private string SubstituteVars(string expr)
    {
        foreach (var (name, value) in _vars)
            expr = System.Text.RegularExpressions.Regex.Replace(
                expr, @"\b" + System.Text.RegularExpressions.Regex.Escape(name) + @"\b",
                value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return expr;
    }

    private double EvalExpr(string expr)
    {
        // Simple recursive-descent parser (same core as FormaScript)
        var p = new ExprParser(expr);
        return p.Parse();
    }

    private sealed class ExprParser(string input)
    {
        private int _pos;

        public double Parse()
        {
            var val = ParseAddSub();
            if (_pos < input.Length) throw new Exception($"Unexpected '{input[_pos]}'");
            return val;
        }

        private double ParseAddSub()
        {
            var left = ParseMulDiv();
            while (_pos < input.Length && (input[_pos] == '+' || input[_pos] == '-'))
            {
                var op = input[_pos++];
                var right = ParseMulDiv();
                left = op == '+' ? left + right : left - right;
            }
            return left;
        }

        private double ParseMulDiv()
        {
            var left = ParsePow();
            while (_pos < input.Length && (input[_pos] == '*' || input[_pos] == '/'))
            {
                var op = input[_pos++];
                var right = ParsePow();
                left = op == '*' ? left * right : left / right;
            }
            return left;
        }

        private double ParsePow()
        {
            var left = ParseUnary();
            if (_pos < input.Length && input[_pos] == '^')
            {
                _pos++;
                var exp = ParseUnary();
                left = Math.Pow(left, exp);
            }
            return left;
        }

        private double ParseUnary()
        {
            SkipWs();
            if (_pos < input.Length && input[_pos] == '-') { _pos++; return -ParsePrimary(); }
            if (_pos < input.Length && input[_pos] == '+') { _pos++; return ParsePrimary(); }
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWs();
            if (_pos >= input.Length) throw new Exception("Unexpected end of expression");

            if (input[_pos] == '(')
            {
                _pos++;
                var val = ParseAddSub();
                SkipWs();
                if (_pos < input.Length && input[_pos] == ')') _pos++;
                return val;
            }

            if (char.IsLetter(input[_pos]) || input[_pos] == '_')
            {
                var start = _pos;
                while (_pos < input.Length && (char.IsLetterOrDigit(input[_pos]) || input[_pos] == '_')) _pos++;
                var name = input[start.._pos];
                SkipWs();
                if (_pos < input.Length && input[_pos] == '(')
                {
                    _pos++;
                    var args = new List<double>();
                    while (_pos < input.Length && input[_pos] != ')')
                    {
                        args.Add(ParseAddSub());
                        SkipWs();
                        if (_pos < input.Length && input[_pos] == ',') _pos++;
                    }
                    if (_pos < input.Length) _pos++;
                    return name.ToLowerInvariant() switch
                    {
                        "sqrt" => Math.Sqrt(args[0]),
                        "abs"  => Math.Abs(args[0]),
                        "sin"  => Math.Sin(args[0] * Math.PI / 180),
                        "cos"  => Math.Cos(args[0] * Math.PI / 180),
                        "tan"  => Math.Tan(args[0] * Math.PI / 180),
                        "min"  => args.Count >= 2 ? Math.Min(args[0], args[1]) : args[0],
                        "max"  => args.Count >= 2 ? Math.Max(args[0], args[1]) : args[0],
                        "pow"  => args.Count >= 2 ? Math.Pow(args[0], args[1]) : args[0],
                        "floor" => Math.Floor(args[0]),
                        "ceil"  => Math.Ceiling(args[0]),
                        "round" => Math.Round(args[0]),
                        _ => throw new Exception($"Unknown function '{name}'")
                    };
                }
                return name.ToLowerInvariant() switch
                {
                    "pi"  => Math.PI,
                    "tau" => Math.Tau,
                    "e"   => Math.E,
                    _ => throw new Exception($"Unknown identifier '{name}'")
                };
            }

            var numStart = _pos;
            while (_pos < input.Length && (char.IsDigit(input[_pos]) || input[_pos] == '.' || input[_pos] == 'e' || input[_pos] == 'E' ||
                   ((_pos > numStart) && (input[_pos] == '+' || input[_pos] == '-') && char.ToLower(input[_pos - 1]) == 'e')))
                _pos++;
            return double.Parse(input[numStart.._pos], System.Globalization.CultureInfo.InvariantCulture);
        }

        private void SkipWs() { while (_pos < input.Length && input[_pos] == ' ') _pos++; }
    }
}
