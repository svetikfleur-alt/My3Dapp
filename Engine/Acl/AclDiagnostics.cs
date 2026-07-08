namespace FormaCore.Engine.Acl;

public enum AclDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record AclSourceSpan(int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    public static AclSourceSpan None { get; } = new(0, 0, 0, 0);

    public override string ToString() => StartLine > 0 ? $"{StartLine}:{StartColumn}-{EndLine}:{EndColumn}" : "unknown";
}

public sealed record AclDiagnostic(
    AclDiagnosticSeverity Severity,
    string Category,
    string Message,
    AclSourceSpan Span)
{
    public override string ToString() => $"{Severity} [{Category}] at {Span}: {Message}";
}
