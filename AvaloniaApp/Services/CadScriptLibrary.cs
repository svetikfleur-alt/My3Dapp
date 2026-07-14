using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace My3DApp.AvaloniaApp.Services;

public static class CadScriptLibrary
{
    /// <summary>
    /// Small ACL script used by the "Run sample ACL" empty-state action and the
    /// headless smoke runner. It must stay valid against the current parser.
    /// </summary>
    public const string SampleAclScript = """
        let width = 60
        let depth = 40
        let height = 25
        select plane top
        start sketch
        rectangle 0 0 ${width} ${depth}
        finish sketch
        extrude ${height}
        fillet 3
        """;

    private static readonly Regex SequenceSeparatorPattern = new(
        @"(?:\r?\n|;|->|\bthen\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommentPattern = new(
        @"(?://|#).*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex VariablePattern = new(
        @"\$(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    private static readonly Regex ExpressionInterpolationPattern = new(
        @"\$\{(?<expr>[^}]+)\}",
        RegexOptions.Compiled);

    private static readonly Regex LetPattern = new(
        @"^(?:let|set)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<expr>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RepeatPattern = new(
        @"^repeat\s+(?<count>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForPattern = new(
        @"^for\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+in\s+(?<start>.+?)\s*\.\.\s*(?<end>.+?)(?:\s+step\s+(?<step>.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DefPattern = new(
        @"^(?:def|function)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<params>.*)\)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InvocationPattern = new(
        @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)$",
        RegexOptions.Compiled);

    public static string ExpandSequence(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        if (TryExpandSequence(input, out var expanded, out _))
        {
            return expanded;
        }

        return ExpandSequenceLegacy(input);
    }

    public static bool TryExpandSequence(string input, out string expanded, out string? error)
    {
        expanded = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            expanded = input;
            return true;
        }

        try
        {
            var tokens = TokenizeScript(input);
            if (tokens.Count == 0)
            {
                expanded = string.Empty;
                return true;
            }

            var macros = new Dictionary<string, CadScriptMacro>(StringComparer.OrdinalIgnoreCase);
            var context = new CadScriptScope(null);
            var lines = ExecuteBlock(tokens, macros, context);
            expanded = string.Join("; ", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string ExpandLine(string line)
    {
        var trimmed = NormalizePart(line);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (MakerTemplateLibrary.TryExpandInvocation(trimmed, out var expandedTemplate))
        {
            return expandedTemplate;
        }

        if (CadRecipeLibrary.TryExpandLine(trimmed, out var expandedRecipe))
        {
            return expandedRecipe;
        }

        return trimmed;
    }

    private static string ExpandSequenceLegacy(string input)
    {
        var normalized = NormalizeScriptEnvelope(input);
        var parts = SequenceSeparatorPattern.Split(normalized);
        var result = new StringBuilder();
        var first = true;

        foreach (var rawPart in parts)
        {
            var part = NormalizePart(rawPart);
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var expanded = ExpandLine(part);
            if (!first)
            {
                result.Append("; ");
            }

            result.Append(expanded);
            first = false;
        }

        return result.ToString();
    }

    private static List<string> TokenizeScript(string input)
    {
        var normalized = NormalizeScriptEnvelope(input);
        normalized = InsertBlockDelimiters(normalized);

        var tokens = new List<string>();
        foreach (var rawLine in normalized.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = NormalizePart(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (string.Equals(line, "{", StringComparison.Ordinal) || string.Equals(line, "}", StringComparison.Ordinal))
            {
                tokens.Add(line);
                continue;
            }

            foreach (var part in SplitTopLevel(line))
            {
                var token = NormalizePart(part);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token);
                }
            }
        }

        return tokens;
    }

    private static string InsertBlockDelimiters(string text)
    {
        var builder = new StringBuilder(text.Length * 2);
        var inInterpolation = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (!inInterpolation && ch == '$' && i + 1 < text.Length && text[i + 1] == '{')
            {
                inInterpolation = true;
                builder.Append(ch);
                builder.Append('{');
                i++;
                continue;
            }

            if (inInterpolation)
            {
                builder.Append(ch);
                if (ch == '}')
                {
                    inInterpolation = false;
                }

                continue;
            }

            if (ch == '{' || ch == '}')
            {
                builder.AppendLine();
                builder.Append(ch);
                builder.AppendLine();
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ExecuteBlock(
        IReadOnlyList<string> tokens,
        Dictionary<string, CadScriptMacro> macros,
        CadScriptScope scope)
    {
        var index = 0;
        return ExecuteBlock(tokens, macros, scope, ref index);
    }

    private static List<string> ExecuteBlock(
        IReadOnlyList<string> tokens,
        Dictionary<string, CadScriptMacro> macros,
        CadScriptScope scope,
        ref int index)
    {
        var output = new List<string>();

        while (index < tokens.Count)
        {
            var token = tokens[index++];
            if (string.Equals(token, "}", StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(token, "{", StringComparison.Ordinal))
            {
                continue;
            }

            var letMatch = LetPattern.Match(token);
            if (letMatch.Success)
            {
                var name = letMatch.Groups["name"].Value;
                var value = EvaluateExpression(letMatch.Groups["expr"].Value, scope);
                scope.Set(name, value);
                continue;
            }

            var repeatMatch = RepeatPattern.Match(token);
            if (repeatMatch.Success)
            {
                var count = (int)Math.Max(0, Math.Round(EvaluateExpression(repeatMatch.Groups["count"].Value, scope), MidpointRounding.AwayFromZero));
                var blockTokens = ReadBraceBlock(tokens, ref index);
                for (var iteration = 0; iteration < count; iteration++)
                {
                    var loopScope = new CadScriptScope(scope);
                    loopScope.Set("index", iteration);
                    loopScope.Set("iteration", iteration + 1);
                    var nestedIndex = 0;
                    output.AddRange(ExecuteBlock(blockTokens, macros, loopScope, ref nestedIndex));
                }

                continue;
            }

            var forMatch = ForPattern.Match(token);
            if (forMatch.Success)
            {
                var variableName = forMatch.Groups["name"].Value;
                var start = EvaluateExpression(forMatch.Groups["start"].Value, scope);
                var end = EvaluateExpression(forMatch.Groups["end"].Value, scope);
                var step = forMatch.Groups["step"].Success
                    ? EvaluateExpression(forMatch.Groups["step"].Value, scope)
                    : start <= end ? 1d : -1d;
                if (Math.Abs(step) < 0.000001d)
                {
                    step = 1d;
                }

                var blockTokens = ReadBraceBlock(tokens, ref index);
                if (step > 0d)
                {
                    for (var value = start; value <= end + 0.000001d; value += step)
                    {
                        var loopScope = new CadScriptScope(scope);
                        loopScope.Set(variableName, value);
                        var nestedIndex = 0;
                        output.AddRange(ExecuteBlock(blockTokens, macros, loopScope, ref nestedIndex));
                    }
                }
                else
                {
                    for (var value = start; value >= end - 0.000001d; value += step)
                    {
                        var loopScope = new CadScriptScope(scope);
                        loopScope.Set(variableName, value);
                        var nestedIndex = 0;
                        output.AddRange(ExecuteBlock(blockTokens, macros, loopScope, ref nestedIndex));
                    }
                }

                continue;
            }

            var defMatch = DefPattern.Match(token);
            if (defMatch.Success)
            {
                var name = defMatch.Groups["name"].Value;
                var parameterSpecs = ParseParameterSpecs(defMatch.Groups["params"].Value);
                var body = ReadBraceBlock(tokens, ref index);
                macros[name] = new CadScriptMacro(name, parameterSpecs, body);
                continue;
            }

            var invocationMatch = InvocationPattern.Match(token);
            if (invocationMatch.Success &&
                macros.TryGetValue(invocationMatch.Groups["name"].Value, out var macro))
            {
                var args = ParseArgumentAssignments(invocationMatch.Groups["args"].Value, scope);
                var macroScope = new CadScriptScope(scope);
                BindMacroArguments(macro, args, macroScope, scope);
                var macroIndex = 0;
                output.AddRange(ExecuteBlock(macro.Body, macros, macroScope, ref macroIndex));
                continue;
            }

            var expandedLine = ExpandInterpolatedLine(token, scope);
            var expandedCommand = ExpandLine(expandedLine);
            if (!string.Equals(expandedCommand, expandedLine, StringComparison.Ordinal))
            {
                expandedCommand = ExpandSequence(expandedCommand);
            }

            if (!string.IsNullOrWhiteSpace(expandedCommand))
            {
                output.Add(expandedCommand);
            }
        }

        return output;
    }

    private static IReadOnlyList<string> ReadBraceBlock(IReadOnlyList<string> tokens, ref int index)
    {
        if (index >= tokens.Count || !string.Equals(tokens[index], "{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Expected '{' to open a script block.");
        }

        index++;
        var depth = 1;
        var start = index;
        while (index < tokens.Count && depth > 0)
        {
            if (string.Equals(tokens[index], "{", StringComparison.Ordinal))
            {
                depth++;
            }
            else if (string.Equals(tokens[index], "}", StringComparison.Ordinal))
            {
                depth--;
            }

            index++;
        }

        if (depth != 0)
        {
            throw new InvalidOperationException("Missing closing brace in ACL script.");
        }

        return tokens.Skip(start).Take(index - start - 1).ToArray();
    }

    private static IReadOnlyList<CadScriptParameterSpec> ParseParameterSpecs(string text)
    {
        var specs = new List<CadScriptParameterSpec>();
        foreach (var part in SplitTopLevel(text, ','))
        {
            var token = NormalizePart(part);
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var pieces = token.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 2)
            {
                specs.Add(new CadScriptParameterSpec(pieces[0], pieces[1]));
            }
            else
            {
                specs.Add(new CadScriptParameterSpec(token, null));
            }
        }

        return specs;
    }

    private static IReadOnlyList<CadScriptArgument> ParseArgumentAssignments(string text, CadScriptScope scope)
    {
        var arguments = new List<CadScriptArgument>();
        foreach (var part in SplitTopLevel(text, ','))
        {
            var token = NormalizePart(part);
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var pieces = token.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 2)
            {
                arguments.Add(new CadScriptArgument(pieces[0], EvaluateExpression(pieces[1], scope), true));
            }
            else
            {
                arguments.Add(new CadScriptArgument(string.Empty, EvaluateExpression(token, scope), false));
            }
        }

        return arguments;
    }

    private static void BindMacroArguments(
        CadScriptMacro macro,
        IReadOnlyList<CadScriptArgument> arguments,
        CadScriptScope targetScope,
        CadScriptScope parentScope)
    {
        var positionalIndex = 0;
        foreach (var parameter in macro.Parameters)
        {
            var named = arguments.FirstOrDefault(arg =>
                arg.IsNamed &&
                string.Equals(arg.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                targetScope.Set(parameter.Name, named.Value);
                continue;
            }

            while (positionalIndex < arguments.Count && arguments[positionalIndex].IsNamed)
            {
                positionalIndex++;
            }

            if (positionalIndex < arguments.Count)
            {
                targetScope.Set(parameter.Name, arguments[positionalIndex].Value);
                positionalIndex++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parameter.DefaultExpression))
            {
                targetScope.Set(parameter.Name, EvaluateExpression(parameter.DefaultExpression!, parentScope));
                continue;
            }

            targetScope.Set(parameter.Name, 0d);
        }
    }

    private static string ExpandInterpolatedLine(string line, CadScriptScope scope)
    {
        var expanded = ExpressionInterpolationPattern.Replace(line, match =>
        {
            var expr = match.Groups["expr"].Value;
            return FormatNumber(EvaluateExpression(expr, scope));
        });

        expanded = VariablePattern.Replace(expanded, match =>
        {
            var name = match.Groups["name"].Value;
            return FormatNumber(scope.Get(name));
        });

        return expanded;
    }

    private static double EvaluateExpression(string expression, CadScriptScope scope) =>
        new ExpressionParser(expression, scope).Parse();

    private static IEnumerable<string> SplitTopLevel(string text, char separator = ';')
    {
        var depth = 0;
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '(':
                    depth++;
                    builder.Append(ch);
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    builder.Append(ch);
                    break;
                default:
                    if (ch == separator && depth == 0)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string NormalizeScriptEnvelope(string input)
    {
        var stripped = input.Replace("```umx1", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```cad", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase);
        stripped = CommentPattern.Replace(stripped, string.Empty);

        var lines = stripped
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(NormalizePart)
            .Where(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !string.Equals(line, "umx1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line, "cad", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line, "script", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line, "program", StringComparison.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizePart(string value) =>
        value.Trim().TrimEnd(',');

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed class CadScriptScope
    {
        private readonly CadScriptScope? _parent;
        private readonly Dictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);

        public CadScriptScope(CadScriptScope? parent)
        {
            _parent = parent;
        }

        public void Set(string name, double value) => _values[name] = value;

        public double Get(string name)
        {
            if (_values.TryGetValue(name, out var local))
            {
                return local;
            }

            if (_parent is not null)
            {
                return _parent.Get(name);
            }

            throw new InvalidOperationException($"Unknown ACL variable '{name}'.");
        }
    }

    private sealed record CadScriptParameterSpec(string Name, string? DefaultExpression);

    private sealed record CadScriptArgument(string Name, double Value, bool IsNamed);

    private sealed record CadScriptMacro(
        string Name,
        IReadOnlyList<CadScriptParameterSpec> Parameters,
        IReadOnlyList<string> Body);

    private sealed class ExpressionParser
    {
        private readonly string _text;
        private readonly CadScriptScope _scope;
        private int _index;

        public ExpressionParser(string text, CadScriptScope scope)
        {
            _text = text.Trim();
            _scope = scope;
        }

        public double Parse()
        {
            var value = ParseAddSubtract();
            SkipWhitespace();
            if (_index < _text.Length)
            {
                throw new InvalidOperationException($"Unexpected token in expression '{_text}'.");
            }

            return value;
        }

        private double ParseAddSubtract()
        {
            var value = ParseMultiplyDivide();
            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                {
                    value += ParseMultiplyDivide();
                }
                else if (Match('-'))
                {
                    value -= ParseMultiplyDivide();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseMultiplyDivide()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (Match('*'))
                {
                    value *= ParseUnary();
                }
                else if (Match('/'))
                {
                    value /= ParseUnary();
                }
                else if (Match('%'))
                {
                    value %= ParseUnary();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (Match('+'))
            {
                return ParseUnary();
            }

            if (Match('-'))
            {
                return -ParseUnary();
            }

            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (Match('('))
            {
                var value = ParseAddSubtract();
                Expect(')');
                return value;
            }

            if (char.IsLetter(Current) || Current == '_')
            {
                var identifier = ReadIdentifier();
                SkipWhitespace();
                if (Match('('))
                {
                    var args = new List<double>();
                    SkipWhitespace();
                    if (!Peek(')'))
                    {
                        do
                        {
                            args.Add(ParseAddSubtract());
                            SkipWhitespace();
                        }
                        while (Match(','));
                    }

                    Expect(')');
                    return EvaluateFunction(identifier, args);
                }

                return _scope.Get(identifier);
            }

            return ReadNumber();
        }

        private double EvaluateFunction(string name, IReadOnlyList<double> args)
        {
            return name.ToLowerInvariant() switch
            {
                "min" => args.Min(),
                "max" => args.Max(),
                "abs" => Math.Abs(args[0]),
                "round" => Math.Round(args[0], MidpointRounding.AwayFromZero),
                "floor" => Math.Floor(args[0]),
                "ceil" or "ceiling" => Math.Ceiling(args[0]),
                "clamp" => Math.Clamp(args[0], args[1], args[2]),
                "pow" => Math.Pow(args[0], args[1]),
                "sqrt" => Math.Sqrt(args[0]),
                _ => throw new InvalidOperationException($"Unknown ACL function '{name}'.")
            };
        }

        private double ReadNumber()
        {
            SkipWhitespace();
            var start = _index;
            var seenSeparator = false;
            while (_index < _text.Length)
            {
                var ch = _text[_index];
                if (char.IsDigit(ch))
                {
                    _index++;
                    continue;
                }

                if ((ch == '.' || ch == ',') &&
                    !seenSeparator &&
                    _index + 1 < _text.Length &&
                    char.IsDigit(_text[_index + 1]))
                {
                    seenSeparator = true;
                    _index++;
                    continue;
                }

                break;
            }

            var slice = _text[start.._index].Replace(',', '.');
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException($"Invalid number in expression '{_text}'.");
            }

            return value;
        }

        private string ReadIdentifier()
        {
            var start = _index;
            while (_index < _text.Length && (char.IsLetterOrDigit(_text[_index]) || _text[_index] == '_'))
            {
                _index++;
            }

            return _text[start.._index];
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        private bool Match(char value)
        {
            SkipWhitespace();
            if (_index < _text.Length && _text[_index] == value)
            {
                _index++;
                return true;
            }

            return false;
        }

        private bool Peek(char value)
        {
            SkipWhitespace();
            return _index < _text.Length && _text[_index] == value;
        }

        private void Expect(char value)
        {
            if (!Match(value))
            {
                throw new InvalidOperationException($"Expected '{value}' in expression '{_text}'.");
            }
        }

        private char Current => _index < _text.Length ? _text[_index] : '\0';
    }
}
