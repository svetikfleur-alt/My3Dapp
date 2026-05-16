using System.Text;
using System.Text.RegularExpressions;

namespace My3DApp.AvaloniaApp.Services;

public static class CadScriptLibrary
{
    private static readonly Regex SequenceSeparatorPattern = new(
        @"(?:\r?\n|;|->|\bthen\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommentPattern = new(
        @"(?://|#).*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static string ExpandSequence(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

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
                !string.Equals(line, "program", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line, "{", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line, "}", StringComparison.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizePart(string value) =>
        value.Trim().TrimEnd(',');
}
