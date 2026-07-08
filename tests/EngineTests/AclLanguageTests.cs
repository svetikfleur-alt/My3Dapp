using FormaCore.Engine.Acl;

namespace FormaCore.Tests.EngineTests;

public sealed class AclLanguageTests
{
    [Fact]
    public void Lexer_ValidTokens_TokenizesCorrectly()
    {
        var source = "let width = 120mm; part plate { box(width: width); } // comment";
        var lexer = new AclLexer(source);
        
        var tokens = new List<AclToken>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Kind == AclTokenKind.EndOfFile) break;
        }

        Assert.Empty(lexer.Diagnostics);
        Assert.Equal(AclTokenKind.KeywordLet, tokens[0].Kind);
        Assert.Equal(AclTokenKind.Identifier, tokens[1].Kind);
        Assert.Equal(AclTokenKind.Equals, tokens[2].Kind);
        Assert.Equal(AclTokenKind.Number, tokens[3].Kind);
        Assert.Equal(120, tokens[3].NumericValue);
        Assert.Equal("mm", tokens[3].Unit);
        Assert.Equal(AclTokenKind.Semicolon, tokens[4].Kind);
        Assert.Equal(AclTokenKind.KeywordPart, tokens[5].Kind);
        Assert.Equal(AclTokenKind.Identifier, tokens[6].Kind);
        Assert.Equal(AclTokenKind.OpenBrace, tokens[7].Kind);
        Assert.Equal(AclTokenKind.Identifier, tokens[8].Kind); // box
        Assert.Equal(AclTokenKind.OpenParen, tokens[9].Kind);
        Assert.Equal(AclTokenKind.Identifier, tokens[10].Kind); // width
        Assert.Equal(AclTokenKind.Colon, tokens[11].Kind);
        Assert.Equal(AclTokenKind.Identifier, tokens[12].Kind); // width
        Assert.Equal(AclTokenKind.CloseParen, tokens[13].Kind);
        Assert.Equal(AclTokenKind.Semicolon, tokens[14].Kind);
        Assert.Equal(AclTokenKind.CloseBrace, tokens[15].Kind);
        Assert.Equal(AclTokenKind.EndOfFile, tokens[16].Kind);
    }

    [Fact]
    public void Parser_ValidDocument_ParsesCorrectly()
    {
        var source = "let width = 120; part plate { box(width: width, depth: 80); }";
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var document = parser.ParseDocument();

        Assert.Empty(parser.Diagnostics);
        Assert.Equal(2, document.Nodes.Count);
        
        var letDecl = Assert.IsType<AclLetDeclaration>(document.Nodes[0]);
        Assert.Equal("width", letDecl.Name);
        var num = Assert.IsType<AclNumberExpression>(letDecl.Value);
        Assert.Equal(120, num.Value);
        
        var partDecl = Assert.IsType<AclPartDeclaration>(document.Nodes[1]);
        Assert.Equal("plate", partDecl.Name);
        Assert.Single(partDecl.Statements);
        
        var exprStmt = Assert.IsType<AclExpressionStatement>(partDecl.Statements[0]);
        var call = Assert.IsType<AclCallExpression>(exprStmt.Expression);
        Assert.Equal("box", call.FunctionName);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Equal("width", call.Arguments[0].Name);
        Assert.Equal("depth", call.Arguments[1].Name);
    }

    [Fact]
    public void Validator_ValidDocument_NoDiagnostics()
    {
        var source = "let w = 100mm; let d = 5cm; part p { box(width: w, depth: d, height: 10); }";
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var document = parser.ParseDocument();
        
        var validator = new AclSemanticValidator();
        var result = validator.Validate(document);

        Assert.Empty(parser.Diagnostics);
        Assert.Empty(result.Diagnostics);
        Assert.True(result.IsValid);
        Assert.Equal(100.0, result.ParameterValues["w"]);
        Assert.Equal(50.0, result.ParameterValues["d"]); // cm -> mm
    }

    [Fact]
    public void Validator_InvalidDocument_ReportsDiagnostics()
    {
        var source = "let w = 100hz; part p { box(width: w); sphere(r: 10); }";
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var document = parser.ParseDocument();
        
        var validator = new AclSemanticValidator();
        var result = validator.Validate(document);

        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("unknown unit"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Missing required argument 'depth'"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Missing required argument 'height'"));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Unknown feature 'sphere'"));
    }
}
