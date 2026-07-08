using System.Globalization;
using System.Text;

namespace FormaCore.Engine.Acl;

public enum AclTokenKind
{
    EndOfFile,
    Identifier,
    Number,
    String,
    KeywordLet,
    KeywordPart,
    KeywordSketch,
    KeywordOn,
    KeywordTrue,
    KeywordFalse,
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    Semicolon,
    Colon,
    Comma,
    Equals,
    Plus,
    Minus,
    Asterisk,
    Slash,
    Error
}

public sealed record AclToken(AclTokenKind Kind, string Text, AclSourceSpan Span)
{
    public double NumericValue { get; init; }
    public string? Unit { get; init; }
    public string? StringValue { get; init; }
}

public sealed class AclLexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;
    private readonly List<AclDiagnostic> _diagnostics = [];

    public AclLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public IReadOnlyList<AclDiagnostic> Diagnostics => _diagnostics;

    private char Current => _position < _source.Length ? _source[_position] : '\0';
    private char Peek(int offset) => _position + offset < _source.Length ? _source[_position + offset] : '\0';

    private void Advance()
    {
        if (_position >= _source.Length) return;
        
        if (_source[_position] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        _position++;
    }

    public AclToken NextToken()
    {
        SkipWhitespaceAndComments();

        if (_position >= _source.Length)
        {
            return new AclToken(AclTokenKind.EndOfFile, string.Empty, new AclSourceSpan(_line, _column, _line, _column));
        }

        var startLine = _line;
        var startColumn = _column;
        var startPos = _position;
        var ch = Current;

        if (char.IsLetter(ch) || ch == '_')
        {
            return LexIdentifierOrKeyword(startLine, startColumn);
        }

        if (char.IsDigit(ch) || (ch == '.' && char.IsDigit(Peek(1))))
        {
            return LexNumber(startLine, startColumn);
        }

        if (ch == '"')
        {
            return LexString(startLine, startColumn);
        }

        Advance();
        var span = new AclSourceSpan(startLine, startColumn, _line, _column);
        var text = ch.ToString();

        return ch switch
        {
            '(' => new AclToken(AclTokenKind.OpenParen, text, span),
            ')' => new AclToken(AclTokenKind.CloseParen, text, span),
            '{' => new AclToken(AclTokenKind.OpenBrace, text, span),
            '}' => new AclToken(AclTokenKind.CloseBrace, text, span),
            ';' => new AclToken(AclTokenKind.Semicolon, text, span),
            ':' => new AclToken(AclTokenKind.Colon, text, span),
            ',' => new AclToken(AclTokenKind.Comma, text, span),
            '=' => new AclToken(AclTokenKind.Equals, text, span),
            '+' => new AclToken(AclTokenKind.Plus, text, span),
            '-' => new AclToken(AclTokenKind.Minus, text, span),
            '*' => new AclToken(AclTokenKind.Asterisk, text, span),
            '/' => new AclToken(AclTokenKind.Slash, text, span),
            _ => ReportErrorAndCreateToken($"Unexpected character '{ch}'.", text, span)
        };
    }

    private void SkipWhitespaceAndComments()
    {
        while (_position < _source.Length)
        {
            if (char.IsWhiteSpace(Current))
            {
                Advance();
            }
            else if (Current == '/' && Peek(1) == '/')
            {
                while (_position < _source.Length && Current != '\n')
                {
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private AclToken LexIdentifierOrKeyword(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        while (char.IsLetterOrDigit(Current) || Current == '_')
        {
            sb.Append(Current);
            Advance();
        }

        var text = sb.ToString();
        var span = new AclSourceSpan(startLine, startColumn, _line, _column);

        var kind = text switch
        {
            "let" => AclTokenKind.KeywordLet,
            "part" => AclTokenKind.KeywordPart,
            "sketch" => AclTokenKind.KeywordSketch,
            "on" => AclTokenKind.KeywordOn,
            "true" => AclTokenKind.KeywordTrue,
            "false" => AclTokenKind.KeywordFalse,
            _ => AclTokenKind.Identifier
        };

        return new AclToken(kind, text, span);
    }

    private AclToken LexNumber(int startLine, int startColumn)
    {
        var sb = new StringBuilder();
        var hasDot = false;

        while (char.IsDigit(Current) || (!hasDot && Current == '.'))
        {
            if (Current == '.') hasDot = true;
            sb.Append(Current);
            Advance();
        }

        var numText = sb.ToString();
        string? unit = null;

        if (char.IsLetter(Current))
        {
            var unitSb = new StringBuilder();
            while (char.IsLetter(Current))
            {
                unitSb.Append(Current);
                Advance();
            }
            unit = unitSb.ToString();
        }

        var span = new AclSourceSpan(startLine, startColumn, _line, _column);
        var text = unit == null ? numText : numText + unit;

        if (!double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return ReportErrorAndCreateToken($"Invalid number format '{numText}'.", text, span);
        }

        return new AclToken(AclTokenKind.Number, text, span)
        {
            NumericValue = value,
            Unit = unit
        };
    }

    private AclToken LexString(int startLine, int startColumn)
    {
        Advance(); // Skip opening quote
        var sb = new StringBuilder();

        while (_position < _source.Length && Current != '"' && Current != '\n')
        {
            sb.Append(Current);
            Advance();
        }

        if (Current != '"')
        {
            var errSpan = new AclSourceSpan(startLine, startColumn, _line, _column);
            return ReportErrorAndCreateToken("Unterminated string literal.", $"\"{sb}", errSpan);
        }

        Advance(); // Skip closing quote
        var span = new AclSourceSpan(startLine, startColumn, _line, _column);
        return new AclToken(AclTokenKind.String, $"\"{sb}\"", span)
        {
            StringValue = sb.ToString()
        };
    }

    private AclToken ReportErrorAndCreateToken(string message, string text, AclSourceSpan span)
    {
        _diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Lexical", message, span));
        return new AclToken(AclTokenKind.Error, text, span);
    }
}
