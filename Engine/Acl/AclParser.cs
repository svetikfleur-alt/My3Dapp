namespace FormaCore.Engine.Acl;

public sealed class AclParser
{
    private readonly List<AclToken> _tokens = [];
    private int _position;
    private readonly List<AclDiagnostic> _diagnostics = [];

    public AclParser(AclLexer lexer)
    {
        while (true)
        {
            var token = lexer.NextToken();
            _tokens.Add(token);
            if (token.Kind == AclTokenKind.EndOfFile)
            {
                break;
            }
        }
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    public IReadOnlyList<AclDiagnostic> Diagnostics => _diagnostics;

    private AclToken Current => _position < _tokens.Count ? _tokens[_position] : _tokens.Last();
    private AclToken Peek(int offset) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens.Last();

    private AclToken Advance()
    {
        var current = Current;
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }
        return current;
    }

    private bool Match(AclTokenKind kind)
    {
        if (Current.Kind == kind)
        {
            Advance();
            return true;
        }
        return false;
    }

    private AclToken Consume(AclTokenKind kind, string errorMessage)
    {
        if (Current.Kind == kind)
        {
            return Advance();
        }
        
        ReportError(errorMessage, Current.Span);
        return new AclToken(kind, string.Empty, Current.Span);
    }

    private void ReportError(string message, AclSourceSpan span)
    {
        _diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Syntax", message, span));
    }

    public AclDocument ParseDocument()
    {
        var nodes = new List<AclNode>();
        while (Current.Kind != AclTokenKind.EndOfFile)
        {
            try
            {
                if (Current.Kind == AclTokenKind.KeywordLet)
                {
                    nodes.Add(ParseLetDeclaration());
                }
                else if (Current.Kind == AclTokenKind.KeywordPart)
                {
                    nodes.Add(ParsePartDeclaration());
                }
                else
                {
                    ReportError($"Unexpected token '{Current.Text}' at top level.", Current.Span);
                    RecoverTopLevel();
                }
            }
            catch (ParseException)
            {
                RecoverTopLevel();
            }
        }
        return new AclDocument(nodes);
    }

    private void RecoverTopLevel()
    {
        Advance();
        while (Current.Kind != AclTokenKind.EndOfFile && Current.Kind != AclTokenKind.KeywordLet && Current.Kind != AclTokenKind.KeywordPart)
        {
            Advance();
        }
    }

    private void RecoverStatement()
    {
        while (Current.Kind != AclTokenKind.EndOfFile && Current.Kind != AclTokenKind.Semicolon && Current.Kind != AclTokenKind.CloseBrace)
        {
            Advance();
        }
        if (Current.Kind == AclTokenKind.Semicolon)
        {
            Advance();
        }
    }

    private AclLetDeclaration ParseLetDeclaration()
    {
        var letToken = Consume(AclTokenKind.KeywordLet, "Expected 'let'.");
        var nameToken = Consume(AclTokenKind.Identifier, "Expected variable name after 'let'.");
        Consume(AclTokenKind.Equals, "Expected '=' after variable name.");
        var expr = ParseExpression();
        var semiToken = Consume(AclTokenKind.Semicolon, "Expected ';' after variable declaration.");
        
        return new AclLetDeclaration(nameToken.Text, expr, new AclSourceSpan(letToken.Span.StartLine, letToken.Span.StartColumn, semiToken.Span.EndLine, semiToken.Span.EndColumn));
    }

    private AclPartDeclaration ParsePartDeclaration()
    {
        var partToken = Consume(AclTokenKind.KeywordPart, "Expected 'part'.");
        var nameToken = Consume(AclTokenKind.Identifier, "Expected part name.");
        Consume(AclTokenKind.OpenBrace, "Expected '{' after part name.");

        var statements = new List<AclStatement>();
        while (Current.Kind != AclTokenKind.EndOfFile && Current.Kind != AclTokenKind.CloseBrace)
        {
            try
            {
                statements.Add(ParseStatement());
            }
            catch (ParseException)
            {
                RecoverStatement();
            }
        }

        var closeToken = Consume(AclTokenKind.CloseBrace, "Expected '}' at end of part body.");
        return new AclPartDeclaration(nameToken.Text, statements, new AclSourceSpan(partToken.Span.StartLine, partToken.Span.StartColumn, closeToken.Span.EndLine, closeToken.Span.EndColumn));
    }

    private AclStatement ParseStatement()
    {
        var expr = ParseExpression();
        var semiToken = Consume(AclTokenKind.Semicolon, "Expected ';' after expression statement.");
        return new AclExpressionStatement(expr, new AclSourceSpan(expr.Span.StartLine, expr.Span.StartColumn, semiToken.Span.EndLine, semiToken.Span.EndColumn));
    }

    private AclExpression ParseExpression()
    {
        return ParseBinaryExpression(0);
    }

    private AclExpression ParseBinaryExpression(int precedence)
    {
        var left = ParsePrimary();

        while (true)
        {
            var opPrecedence = GetBinaryOperatorPrecedence(Current.Kind);
            if (opPrecedence == 0 || opPrecedence < precedence)
            {
                break;
            }

            var opToken = Advance();
            var right = ParseBinaryExpression(opPrecedence + 1);
            left = new AclBinaryExpression(left, opToken.Text, right, new AclSourceSpan(left.Span.StartLine, left.Span.StartColumn, right.Span.EndLine, right.Span.EndColumn));
        }

        return left;
    }

    private static int GetBinaryOperatorPrecedence(AclTokenKind kind) => kind switch
    {
        AclTokenKind.Asterisk or AclTokenKind.Slash => 2,
        AclTokenKind.Plus or AclTokenKind.Minus => 1,
        _ => 0
    };

    private AclExpression ParsePrimary()
    {
        if (Match(AclTokenKind.Minus))
        {
            var opToken = _tokens[_position - 1];
            var operand = ParsePrimary();
            return new AclUnaryExpression(opToken.Text, operand, new AclSourceSpan(opToken.Span.StartLine, opToken.Span.StartColumn, operand.Span.EndLine, operand.Span.EndColumn));
        }

        if (Match(AclTokenKind.OpenParen))
        {
            var expr = ParseExpression();
            var closeParen = Consume(AclTokenKind.CloseParen, "Expected ')' after expression.");
            // Unwrap parentheses in AST or represent them. For now, we can just return the expression,
            // but the span might not include parens. Let's just return the inner expression to keep AST clean.
            return expr;
        }

        if (Current.Kind == AclTokenKind.Number)
        {
            var token = Advance();
            return new AclNumberExpression(token.NumericValue, token.Unit, token.Span);
        }

        if (Current.Kind == AclTokenKind.String)
        {
            var token = Advance();
            return new AclStringExpression(token.StringValue ?? string.Empty, token.Span);
        }

        if (Current.Kind == AclTokenKind.KeywordTrue)
        {
            var token = Advance();
            return new AclBooleanExpression(true, token.Span);
        }

        if (Current.Kind == AclTokenKind.KeywordFalse)
        {
            var token = Advance();
            return new AclBooleanExpression(false, token.Span);
        }

        if (Current.Kind == AclTokenKind.Identifier)
        {
            var idToken = Advance();
            
            if (Current.Kind == AclTokenKind.OpenParen)
            {
                return ParseCallExpression(idToken);
            }

            return new AclIdentifierExpression(idToken.Text, idToken.Span);
        }

        ReportError($"Unexpected token '{Current.Text}' in expression.", Current.Span);
        throw new ParseException();
    }

    private AclCallExpression ParseCallExpression(AclToken idToken)
    {
        var openParen = Consume(AclTokenKind.OpenParen, "Expected '(' after function name.");
        var arguments = new List<AclArgument>();

        if (Current.Kind != AclTokenKind.CloseParen)
        {
            while (true)
            {
                string? argName = null;
                var argStartToken = Current;

                if (Current.Kind == AclTokenKind.Identifier && Peek(1).Kind == AclTokenKind.Colon)
                {
                    argName = Advance().Text;
                    Advance(); // consume colon
                }

                var expr = ParseExpression();
                arguments.Add(new AclArgument(argName, expr, new AclSourceSpan(argStartToken.Span.StartLine, argStartToken.Span.StartColumn, expr.Span.EndLine, expr.Span.EndColumn)));

                if (Current.Kind == AclTokenKind.Comma)
                {
                    Advance();
                }
                else
                {
                    break;
                }
            }
        }

        var closeParen = Consume(AclTokenKind.CloseParen, "Expected ')' after arguments.");
        return new AclCallExpression(idToken.Text, arguments, new AclSourceSpan(idToken.Span.StartLine, idToken.Span.StartColumn, closeParen.Span.EndLine, closeParen.Span.EndColumn));
    }

    private sealed class ParseException : Exception { }
}
