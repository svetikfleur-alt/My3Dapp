namespace FormaCore.Engine.Acl;

public sealed class AclDocument
{
    public AclDocument(IReadOnlyList<AclNode> nodes)
    {
        Nodes = nodes;
    }

    public IReadOnlyList<AclNode> Nodes { get; }
}

public abstract class AclNode
{
    protected AclNode(AclSourceSpan span)
    {
        Span = span;
    }

    public AclSourceSpan Span { get; }
}

public sealed class AclLetDeclaration : AclNode
{
    public AclLetDeclaration(string name, AclExpression value, AclSourceSpan span) : base(span)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public AclExpression Value { get; }
}

public sealed class AclPartDeclaration : AclNode
{
    public AclPartDeclaration(string name, IReadOnlyList<AclStatement> statements, AclSourceSpan span) : base(span)
    {
        Name = name;
        Statements = statements;
    }

    public string Name { get; }
    public IReadOnlyList<AclStatement> Statements { get; }
}

public abstract class AclStatement : AclNode
{
    protected AclStatement(AclSourceSpan span) : base(span) { }
}

public sealed class AclExpressionStatement : AclStatement
{
    public AclExpressionStatement(AclExpression expression, AclSourceSpan span) : base(span)
    {
        Expression = expression;
    }

    public AclExpression Expression { get; }
}

public abstract class AclExpression : AclNode
{
    protected AclExpression(AclSourceSpan span) : base(span) { }
}

public sealed class AclNumberExpression : AclExpression
{
    public AclNumberExpression(double value, string? unit, AclSourceSpan span) : base(span)
    {
        Value = value;
        Unit = unit;
    }

    public double Value { get; }
    public string? Unit { get; }
}

public sealed class AclStringExpression : AclExpression
{
    public AclStringExpression(string value, AclSourceSpan span) : base(span)
    {
        Value = value;
    }

    public string Value { get; }
}

public sealed class AclBooleanExpression : AclExpression
{
    public AclBooleanExpression(bool value, AclSourceSpan span) : base(span)
    {
        Value = value;
    }

    public bool Value { get; }
}

public sealed class AclIdentifierExpression : AclExpression
{
    public AclIdentifierExpression(string name, AclSourceSpan span) : base(span)
    {
        Name = name;
    }

    public string Name { get; }
}

public sealed class AclCallExpression : AclExpression
{
    public AclCallExpression(string functionName, IReadOnlyList<AclArgument> arguments, AclSourceSpan span) : base(span)
    {
        FunctionName = functionName;
        Arguments = arguments;
    }

    public string FunctionName { get; }
    public IReadOnlyList<AclArgument> Arguments { get; }
}

public sealed class AclArgument : AclNode
{
    public AclArgument(string? name, AclExpression expression, AclSourceSpan span) : base(span)
    {
        Name = name;
        Expression = expression;
    }

    public string? Name { get; }
    public AclExpression Expression { get; }
}

public sealed class AclBinaryExpression : AclExpression
{
    public AclBinaryExpression(AclExpression left, string operatorToken, AclExpression right, AclSourceSpan span) : base(span)
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public AclExpression Left { get; }
    public string OperatorToken { get; }
    public AclExpression Right { get; }
}

public sealed class AclUnaryExpression : AclExpression
{
    public AclUnaryExpression(string operatorToken, AclExpression operand, AclSourceSpan span) : base(span)
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public string OperatorToken { get; }
    public AclExpression Operand { get; }
}
