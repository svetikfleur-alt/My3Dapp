using FormaCore.Engine.Acl;
using FormaCore.Engine.Exact.Graph;

namespace FormaCore.Engine.Exact;

public sealed class AclExactCompiler
{

    private readonly AclSemanticValidator _validator;

    public AclExactCompiler()
    {
        _validator = new AclSemanticValidator();
    }

    public (ExactFeatureGraph? Graph, IReadOnlyList<AclDiagnostic> Diagnostics) Compile(string source)
    {
        var lexer = new AclLexer(source);
        var parser = new AclParser(lexer);
        var document = parser.ParseDocument();
        
        var validationResult = _validator.Validate(document);
        
        var allDiagnostics = new List<AclDiagnostic>();
        allDiagnostics.AddRange(parser.Diagnostics);
        allDiagnostics.AddRange(validationResult.Diagnostics);

        if (!validationResult.IsValid || parser.Diagnostics.Any(d => d.Severity == AclDiagnosticSeverity.Error))
        {
            return (null, allDiagnostics);
        }

        var graph = new ExactFeatureGraph();
        var featureIndex = 1;

        foreach (var node in document.Nodes)
        {
            if (node is AclPartDeclaration part)
            {
                foreach (var stmt in part.Statements)
                {
                    if (stmt is AclExpressionStatement exprStmt && exprStmt.Expression is AclCallExpression call)
                    {
                        var featureId = $"{part.Name}_feature_{featureIndex++}";
                        var featureNode = CreateFeatureNode(featureId, call, validationResult.ParameterValues);
                        if (featureNode != null)
                        {
                            graph.AddNode(featureNode);
                        }
                    }
                }
            }
        }

        return (graph, allDiagnostics);
    }

    private ExactFeatureNode? CreateFeatureNode(string id, AclCallExpression call, IReadOnlyDictionary<string, double> parameters)
    {
        if (call.FunctionName == "box")
        {
            var width = GetArgumentValue(call, "width", parameters) ?? 10.0;
            var depth = GetArgumentValue(call, "depth", parameters) ?? 10.0;
            var height = GetArgumentValue(call, "height", parameters) ?? 10.0;
            return new ExactBoxNode(id, call.Span, width, depth, height);
        }
        else if (call.FunctionName == "cylinder")
        {
            var radius = GetArgumentValue(call, "radius", parameters) ?? 5.0;
            var height = GetArgumentValue(call, "height", parameters) ?? 10.0;
            return new ExactCylinderNode(id, call.Span, radius, height);
        }

        return null;
    }

    private double? GetArgumentValue(AclCallExpression call, string argName, IReadOnlyDictionary<string, double> parameters)
    {
        var arg = call.Arguments.FirstOrDefault(a => a.Name == argName);
        if (arg == null) return null;

        return Evaluate(arg.Expression, parameters);
    }

    private double? Evaluate(AclExpression expr, IReadOnlyDictionary<string, double> parameters)
    {
        switch (expr)
        {
            case AclNumberExpression num:
                var value = num.Value;
                if (num.Unit == "cm") value *= 10;
                else if (num.Unit == "m") value *= 1000;
                else if (num.Unit == "inch") value *= 25.4;
                return value;
            case AclIdentifierExpression id:
                if (parameters.TryGetValue(id.Name, out var val)) return val;
                return null;
            case AclUnaryExpression unary:
                var operand = Evaluate(unary.Operand, parameters);
                if (operand.HasValue) return unary.OperatorToken == "-" ? -operand.Value : operand.Value;
                return null;
            case AclBinaryExpression binary:
                var left = Evaluate(binary.Left, parameters);
                var right = Evaluate(binary.Right, parameters);
                if (left.HasValue && right.HasValue)
                {
                    return binary.OperatorToken switch
                    {
                        "+" => left.Value + right.Value,
                        "-" => left.Value - right.Value,
                        "*" => left.Value * right.Value,
                        "/" => right.Value == 0 ? 0 : left.Value / right.Value,
                        _ => 0
                    };
                }
                return null;
        }
        return null;
    }
}
