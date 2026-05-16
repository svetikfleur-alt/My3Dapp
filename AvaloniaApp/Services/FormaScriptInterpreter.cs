using System.Globalization;
using System.Text.RegularExpressions;

namespace My3DApp.AvaloniaApp.Services;

/// <summary>Result of executing a FormaScript program.</summary>
public sealed class FormaScriptResult
{
    public bool IsSuccess { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> Commands { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int LinesExecuted { get; init; }

    public static FormaScriptResult Error(string msg) => new() { IsSuccess = false, ErrorMessage = msg };
    public static FormaScriptResult Ok(List<string> cmds, List<string> warnings, int lines) =>
        new() { IsSuccess = true, Commands = cmds, Warnings = warnings, LinesExecuted = lines };
}

/// <summary>
/// FormaScript interpreter — a parametric CAD scripting language.
///
/// Language reference:
///   var name = expr            numeric variable (full arithmetic)
///   repeat N                   loop body N times
///   for i in range(a, b)       i = a, a+1, …, b-1
///   for i in range(a, b, s)    i = a, a+s, …, with step s
///   if expr op expr            conditional block (==, !=, &lt;, >, &lt;=, >=)
///   def name(p1, p2)           function definition
///   name(arg1, arg2)           function call
///   end                        closes any open block
///   // comment                 line comment
///   &lt;any CAD command>          passed through to the command engine
///
/// Built-in functions in expressions:
///   sqrt, abs, floor, ceil, round, sin, cos, tan, log, exp, min, max, clamp
/// Built-in constants: pi, e, tau
/// </summary>
public sealed class FormaScriptInterpreter
{
    // ── Regex patterns ──────────────────────────────────────────────────────
    private static readonly Regex RxVarDecl = new(
        @"^var\s+(?<n>[A-Za-z_]\w*)\s*=\s*(?<expr>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RxAssign = new(
        @"^(?<n>[A-Za-z_]\w*)\s*=\s*(?<expr>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex RxRepeat = new(
        @"^repeat\s+(?<count>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RxFor = new(
        @"^for\s+(?<var>[A-Za-z_]\w*)\s+in\s+range\s*\(\s*(?<args>[^)]+)\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RxIf = new(
        @"^if\s+(?<cond>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RxDef = new(
        @"^def\s+(?<name>[A-Za-z_]\w*)\s*\(\s*(?<params>[^)]*)\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RxCall = new(
        @"^(?<name>[A-Za-z_]\w*)\s*\(\s*(?<args>[^)]*)\s*\)$",
        RegexOptions.Compiled);

    private static readonly Regex RxVarRef = new(
        @"\b(?<n>[A-Za-z_]\w*)\b",
        RegexOptions.Compiled);

    private const int MaxRepeat = 1000;
    private const int MaxOutput = 10000;
    private const int MaxDepth = 64;

    // ── Public API ───────────────────────────────────────────────────────────
    public FormaScriptResult Execute(string script)
    {
        var lines = script.Split('\n').Select(l => l.Trim()).ToArray();
        var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var fns  = new Dictionary<string, FunctionDef>(StringComparer.OrdinalIgnoreCase);
        var output   = new List<string>();
        var warnings = new List<string>();

        try
        {
            int ran = RunBlock(lines, 0, lines.Length, vars, fns, output, warnings, 0);
            return FormaScriptResult.Ok(output, warnings, ran);
        }
        catch (FormaScriptException ex)
        {
            return FormaScriptResult.Error(ex.Message);
        }
    }

    // ── Block executor ───────────────────────────────────────────────────────
    private static int RunBlock(
        string[] lines, int start, int end,
        Dictionary<string, double> vars,
        Dictionary<string, FunctionDef> fns,
        List<string> output, List<string> warnings, int depth)
    {
        if (depth > MaxDepth)
            throw new FormaScriptException("Maximum nesting depth exceeded.");

        int i = start;
        while (i < end)
        {
            var line = StripComment(lines[i]).Trim();
            i++;

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (string.Equals(line, "end", StringComparison.OrdinalIgnoreCase)) break;

            // ── var declaration ─────────────────────────────────────────────
            var mVar = RxVarDecl.Match(line);
            if (mVar.Success)
            {
                vars[mVar.Groups["n"].Value] = Eval(mVar.Groups["expr"].Value, vars, i);
                continue;
            }

            // ── assignment (no var keyword) ─────────────────────────────────
            var mAss = RxAssign.Match(line);
            if (mAss.Success && !IsBlockOpener(line))
            {
                vars[mAss.Groups["n"].Value] = Eval(mAss.Groups["expr"].Value, vars, i);
                continue;
            }

            // ── def ─────────────────────────────────────────────────────────
            var mDef = RxDef.Match(line);
            if (mDef.Success)
            {
                var name = mDef.Groups["name"].Value;
                var pRaw = mDef.Groups["params"].Value;
                var pms  = pRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var blockEnd = FindEnd(lines, i, end, i);
                fns[name] = new FunctionDef(pms, lines[i..blockEnd]);
                i = blockEnd + 1;
                continue;
            }

            // ── repeat ──────────────────────────────────────────────────────
            var mRep = RxRepeat.Match(line);
            if (mRep.Success)
            {
                var n = (int)Math.Round(Eval(mRep.Groups["count"].Value, vars, i));
                if (n < 0) throw new FormaScriptException($"Line {i}: repeat count cannot be negative.");
                if (n > MaxRepeat) throw new FormaScriptException($"Line {i}: repeat {n} exceeds maximum {MaxRepeat}.");
                var blockEnd = FindEnd(lines, i, end, i);
                for (int r = 0; r < n; r++)
                    RunBlock(lines, i, blockEnd, vars, fns, output, warnings, depth + 1);
                i = blockEnd + 1;
                continue;
            }

            // ── for i in range(...) ─────────────────────────────────────────
            var mFor = RxFor.Match(line);
            if (mFor.Success)
            {
                var loopVar  = mFor.Groups["var"].Value;
                var argParts = mFor.Groups["args"].Value
                    .Split(',', StringSplitOptions.TrimEntries)
                    .Select(a => Eval(a, vars, i))
                    .ToArray();

                double lo = argParts.Length >= 1 ? argParts[0] : 0;
                double hi = argParts.Length >= 2 ? argParts[1] : lo;
                double st = argParts.Length >= 3 ? argParts[2] : 1;

                if (Math.Abs(st) < 1e-12)
                    throw new FormaScriptException($"Line {i}: for-loop step cannot be zero.");

                var blockEnd = FindEnd(lines, i, end, i);
                var iterCount = 0;
                for (double v = lo; st > 0 ? v < hi : v > hi; v += st)
                {
                    if (++iterCount > MaxRepeat)
                        throw new FormaScriptException($"Line {i}: for-loop exceeded {MaxRepeat} iterations.");
                    var loopVars = new Dictionary<string, double>(vars, StringComparer.OrdinalIgnoreCase)
                        { [loopVar] = v };
                    RunBlock(lines, i, blockEnd, loopVars, fns, output, warnings, depth + 1);
                    // propagate any outer-var mutations back
                    foreach (var kv in loopVars)
                        if (vars.ContainsKey(kv.Key)) vars[kv.Key] = kv.Value;
                }
                i = blockEnd + 1;
                continue;
            }

            // ── if ──────────────────────────────────────────────────────────
            var mIf = RxIf.Match(line);
            if (mIf.Success)
            {
                var blockEnd = FindEnd(lines, i, end, i);
                if (EvalCond(mIf.Groups["cond"].Value, vars, i))
                    RunBlock(lines, i, blockEnd, vars, fns, output, warnings, depth + 1);
                i = blockEnd + 1;
                continue;
            }

            // ── function call ────────────────────────────────────────────────
            var mCall = RxCall.Match(line);
            if (mCall.Success && fns.TryGetValue(mCall.Groups["name"].Value, out var fn))
            {
                var argVals = mCall.Groups["args"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(a => Eval(a, vars, i))
                    .ToArray();
                if (argVals.Length != fn.Parameters.Length)
                    throw new FormaScriptException(
                        $"Line {i}: '{mCall.Groups["name"].Value}' expects {fn.Parameters.Length} argument(s), got {argVals.Length}.");

                var callVars = new Dictionary<string, double>(vars, StringComparer.OrdinalIgnoreCase);
                for (int p = 0; p < fn.Parameters.Length; p++)
                    callVars[fn.Parameters[p]] = argVals[p];

                RunBlock(fn.Body, 0, fn.Body.Length, callVars, fns, output, warnings, depth + 1);
                continue;
            }

            // ── CAD command with variable substitution ───────────────────────
            var expanded = SubstVars(line, vars);
            if (!string.IsNullOrWhiteSpace(expanded))
            {
                if (output.Count >= MaxOutput)
                    throw new FormaScriptException($"Script produced more than {MaxOutput} commands.");
                output.Add(expanded);
            }
        }
        return i - start;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static int FindEnd(string[] lines, int from, int total, int errLine)
    {
        int d = 1;
        for (int j = from; j < total; j++)
        {
            var l = StripComment(lines[j]).Trim();
            if (IsBlockOpener(l)) d++;
            else if (string.Equals(l, "end", StringComparison.OrdinalIgnoreCase))
            { if (--d == 0) return j; }
        }
        throw new FormaScriptException($"Line {errLine}: block has no matching 'end'.");
    }

    private static bool IsBlockOpener(string l) =>
        RxRepeat.IsMatch(l) || RxFor.IsMatch(l) || RxIf.IsMatch(l) || RxDef.IsMatch(l);

    private static string StripComment(string l)
    {
        var idx = l.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? l[..idx] : l;
    }

    private static string SubstVars(string line, Dictionary<string, double> vars) =>
        RxVarRef.Replace(line, m =>
            vars.TryGetValue(m.Groups["n"].Value, out var v) ? Fmt(v) : m.Value);

    // ── Expression evaluator ─────────────────────────────────────────────────
    private static double Eval(string expr, Dictionary<string, double> vars, int lineNo)
    {
        try { return new ExprParser(expr.Trim(), vars).Parse(); }
        catch (Exception ex) when (ex is not FormaScriptException)
        { throw new FormaScriptException($"Line {lineNo}: cannot evaluate '{expr}': {ex.Message}"); }
    }

    private static bool EvalCond(string cond, Dictionary<string, double> vars, int lineNo)
    {
        foreach (var (op, fn) in new (string, Func<double, double, bool>)[]
        {
            (">=", (a,b)=>a>=b), ("<=", (a,b)=>a<=b),
            ("!=", (a,b)=>Math.Abs(a-b)>1e-10), ("==", (a,b)=>Math.Abs(a-b)<=1e-10),
            (">",  (a,b)=>a>b),  ("<",  (a,b)=>a<b)
        })
        {
            var idx = cond.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;
            return fn(Eval(cond[..idx], vars, lineNo), Eval(cond[(idx+op.Length)..], vars, lineNo));
        }
        return Math.Abs(Eval(cond, vars, lineNo)) > 1e-10;
    }

    private static string Fmt(double v) =>
        v == Math.Truncate(v) && Math.Abs(v) < 1e12
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("G6", CultureInfo.InvariantCulture);

    // ── Recursive-descent expression parser ─────────────────────────────────
    private sealed class ExprParser(string src, Dictionary<string, double> vars)
    {
        private int _p;

        public double Parse() { var v = AddSub(); SkipWs(); if (_p < src.Length) Fail($"unexpected '{src[_p]}'"); return v; }

        private double AddSub()
        {
            var v = MulDiv();
            while (true) { SkipWs(); if (_p >= src.Length) break;
                if      (src[_p]=='+'){_p++; v+=MulDiv();}
                else if (src[_p]=='-'){_p++; v-=MulDiv();}
                else break; }
            return v;
        }

        private double MulDiv()
        {
            var v = Unary();
            while (true) { SkipWs(); if (_p >= src.Length) break;
                if      (src[_p]=='*'){_p++; v*=Unary();}
                else if (src[_p]=='/'){_p++; var d=Unary(); if(Math.Abs(d)<1e-300) Fail("division by zero"); v/=d;}
                else if (src[_p]=='%'){_p++; v%=Unary();}
                else break; }
            return v;
        }

        private double Unary() { SkipWs(); if(_p<src.Length&&src[_p]=='-'){_p++;return-Pow();} if(_p<src.Length&&src[_p]=='+'){_p++;} return Pow(); }

        private double Pow()   { var v=Atom(); SkipWs(); if(_p<src.Length&&src[_p]=='^'){_p++;v=Math.Pow(v,Unary());} return v; }

        private double Atom()
        {
            SkipWs();
            if (_p >= src.Length) Fail("unexpected end of expression");

            if (src[_p] == '(')
            {
                _p++; var v = AddSub(); SkipWs();
                if (_p >= src.Length || src[_p] != ')') Fail("missing ')'");
                _p++; return v;
            }

            if (char.IsDigit(src[_p]) || src[_p] == '.')
            {
                var s = _p;
                while (_p < src.Length && (char.IsDigit(src[_p]) || src[_p] == '.' || src[_p] == 'e' || src[_p] == 'E'
                    || (_p > s && (src[_p] == '+' || src[_p] == '-') && (src[_p-1] == 'e' || src[_p-1] == 'E')))) _p++;
                var tok = src[s.._p];
                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    Fail($"invalid number '{tok}'");
                return n;
            }

            if (char.IsLetter(src[_p]) || src[_p] == '_')
            {
                var s = _p;
                while (_p < src.Length && (char.IsLetterOrDigit(src[_p]) || src[_p] == '_')) _p++;
                var name = src[s.._p];

                SkipWs();
                // Two-arg functions
                if (_p < src.Length && src[_p] == '(')
                {
                    _p++;
                    var a1 = AddSub(); SkipWs();
                    double a2 = 0;
                    bool hasTwoArgs = _p < src.Length && src[_p] == ',';
                    if (hasTwoArgs) { _p++; a2 = AddSub(); SkipWs(); }
                    if (_p < src.Length && src[_p] == ')') _p++;
                    double a3 = 0;
                    bool hasThreeArgs = false;
                    // clamp has 3 args — re-check
                    if (hasTwoArgs && _p < src.Length && src[_p] == ',')
                    { _p++; a3 = AddSub(); SkipWs(); if (_p < src.Length && src[_p] == ')') _p++; hasThreeArgs = true; }

                    return name.ToLowerInvariant() switch
                    {
                        "sqrt"  => Math.Sqrt(a1),
                        "abs"   => Math.Abs(a1),
                        "floor" => Math.Floor(a1),
                        "ceil"  => Math.Ceiling(a1),
                        "round" => Math.Round(a1),
                        "sin"   => Math.Sin(a1 * Math.PI / 180.0),
                        "cos"   => Math.Cos(a1 * Math.PI / 180.0),
                        "tan"   => Math.Tan(a1 * Math.PI / 180.0),
                        "log"   => Math.Log(a1),
                        "log2"  => Math.Log2(a1),
                        "log10" => Math.Log10(a1),
                        "exp"   => Math.Exp(a1),
                        "pow"   => Math.Pow(a1, a2),
                        "min"   => Math.Min(a1, a2),
                        "max"   => Math.Max(a1, a2),
                        "clamp" => Math.Clamp(a1, a2, a3),
                        "lerp"  => a2 + (a3 - a2) * a1,  // lerp(t, from, to)
                        _ => throw new FormaScriptException($"Unknown function '{name}'.")
                    };
                }

                return name.ToLowerInvariant() switch
                {
                    "pi"  => Math.PI,
                    "tau" => 2 * Math.PI,
                    "e"   => Math.E,
                    _ when vars.TryGetValue(name, out var vv) => vv,
                    _ => throw new FormaScriptException($"Undefined variable '{name}'.")
                };
            }

            Fail($"unexpected character '{src[_p]}'");
            return 0;
        }

        private void SkipWs() { while (_p < src.Length && src[_p] == ' ') _p++; }
        private void Fail(string msg) => throw new FormaScriptException(msg);
    }
}

internal sealed record FunctionDef(string[] Parameters, string[] Body);

internal sealed class FormaScriptException(string message) : Exception(message);
