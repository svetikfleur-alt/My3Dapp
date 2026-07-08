namespace FormaCore.Engine.Acl;

public sealed class AclValidationResult
{
    public AclValidationResult(
        IReadOnlyList<AclDiagnostic> diagnostics,
        IReadOnlyDictionary<string, double> parameterValues)
    {
        Diagnostics = diagnostics;
        ParameterValues = parameterValues;
    }

    public IReadOnlyList<AclDiagnostic> Diagnostics { get; }
    public IReadOnlyDictionary<string, double> ParameterValues { get; }

    public bool IsValid => Diagnostics.All(item => item.Severity != AclDiagnosticSeverity.Error);
}

public sealed class AclSemanticValidator
{
    private static readonly HashSet<string> ValidUnits = ["mm", "cm", "m", "inch", "deg", "rad"];
    private readonly HashSet<string> _declaredIdentifiers = new();
    
    public AclValidationResult Validate(AclDocument document)
    {
        var diagnostics = new List<AclDiagnostic>();
        var variables = new Dictionary<string, double>();

        _declaredIdentifiers.Clear();
        foreach (var node in document.Nodes)
        {
            if (node is AclLetDeclaration letDecl)
            {
                _declaredIdentifiers.Add(letDecl.Name);
            }
            else if (node is AclSketchDeclaration sketchDecl)
            {
                _declaredIdentifiers.Add(sketchDecl.Name);
            }
            else if (node is AclPartDeclaration partDecl)
            {
                _declaredIdentifiers.Add(partDecl.Name);
            }
        }

        foreach (var node in document.Nodes)
        {
            if (node is AclLetDeclaration letDecl)
            {
                if (variables.ContainsKey(letDecl.Name))
                {
                    diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Duplicate variable '{letDecl.Name}'.", letDecl.Span));
                }
                else
                {
                    var value = EvaluateConstantExpression(letDecl.Value, variables, diagnostics);
                    if (value.HasValue)
                    {
                        variables[letDecl.Name] = value.Value;
                    }
                }
            }
            else if (node is AclPartDeclaration partDecl)
            {
                ValidatePart(partDecl, variables, diagnostics);
            }
        }

        return new AclValidationResult(diagnostics, variables);
    }

    private void ValidatePart(AclPartDeclaration part, Dictionary<string, double> variables, List<AclDiagnostic> diagnostics)
    {
        foreach (var stmt in part.Statements)
        {
            if (stmt is AclExpressionStatement exprStmt)
            {
                if (exprStmt.Expression is AclCallExpression call)
                {
                    ValidateFeatureCall(call, variables, diagnostics);
                }
                else
                {
                    diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", "Only feature calls are allowed as statements in a part.", exprStmt.Span));
                }
            }
        }
    }

    private void ValidateFeatureCall(AclCallExpression call, Dictionary<string, double> variables, List<AclDiagnostic> diagnostics)
    {
        var allowedFeatures = new HashSet<string> { "box", "cylinder", "rectangle", "circle", "extrude", "hole", "linear_pattern" };
        if (!allowedFeatures.Contains(call.FunctionName))
        {
            diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Unknown feature '{call.FunctionName}'.", call.Span));
            return;
        }

        var argNames = new HashSet<string>();
        foreach (var arg in call.Arguments)
        {
            if (arg.Name != null)
            {
                if (!argNames.Add(arg.Name))
                {
                    diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Duplicate argument '{arg.Name}'.", arg.Span));
                }
            }
            EvaluateConstantExpression(arg.Expression, variables, diagnostics);
        }

        if (call.FunctionName == "box")
        {
            CheckRequiredArgument(call, "width", argNames, diagnostics);
            CheckRequiredArgument(call, "depth", argNames, diagnostics);
            CheckRequiredArgument(call, "height", argNames, diagnostics);
        }
        else if (call.FunctionName == "cylinder")
        {
            CheckRequiredArgument(call, "radius", argNames, diagnostics);
            CheckRequiredArgument(call, "height", argNames, diagnostics);
        }
        else if (call.FunctionName == "rectangle")
        {
            CheckRequiredArgument(call, "width", argNames, diagnostics);
            CheckRequiredArgument(call, "height", argNames, diagnostics);
        }
        else if (call.FunctionName == "circle")
        {
            CheckRequiredArgument(call, "radius", argNames, diagnostics);
        }
        else if (call.FunctionName == "extrude")
        {
            CheckRequiredArgument(call, "profile", argNames, diagnostics);
            CheckRequiredArgument(call, "depth", argNames, diagnostics);
        }
        else if (call.FunctionName == "hole")
        {
            CheckRequiredArgument(call, "target", argNames, diagnostics);
            CheckRequiredArgument(call, "diameter", argNames, diagnostics);
        }
        else if (call.FunctionName == "linear_pattern")
        {
            CheckRequiredArgument(call, "source", argNames, diagnostics);
            CheckRequiredArgument(call, "count", argNames, diagnostics);
            CheckRequiredArgument(call, "spacing", argNames, diagnostics);
        }
    }

    private void CheckRequiredArgument(AclCallExpression call, string argName, HashSet<string> provided, List<AclDiagnostic> diagnostics)
    {
        if (!provided.Contains(argName))
        {
            diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Missing required argument '{argName}' for feature '{call.FunctionName}'.", call.Span));
        }
    }

    private double? EvaluateConstantExpression(AclExpression expr, Dictionary<string, double> variables, List<AclDiagnostic> diagnostics)
    {
        switch (expr)
        {
            case AclNumberExpression num:
                if (num.Unit != null && !ValidUnits.Contains(num.Unit))
                {
                    diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Incompatible or unknown unit '{num.Unit}'.", num.Span));
                    return null;
                }
                
                var value = num.Value;
                if (num.Unit == "cm") value *= 10;
                else if (num.Unit == "m") value *= 1000;
                else if (num.Unit == "inch") value *= 25.4;
                return value;
                
            case AclIdentifierExpression id:
                if (variables.TryGetValue(id.Name, out var val))
                {
                    return val;
                }
                if (_declaredIdentifiers.Contains(id.Name))
                {
                    return null;
                }
                diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", $"Unknown identifier '{id.Name}'.", id.Span));
                return null;
                
            case AclCallExpression call:
                ValidateFeatureCall(call, variables, diagnostics);
                return null;

            case AclUnaryExpression unary:
                var operand = EvaluateConstantExpression(unary.Operand, variables, diagnostics);
                if (operand.HasValue && unary.OperatorToken == "-") return -operand.Value;
                if (operand.HasValue && unary.OperatorToken == "+") return operand.Value;
                return null;
                
            case AclBinaryExpression binary:
                var left = EvaluateConstantExpression(binary.Left, variables, diagnostics);
                var right = EvaluateConstantExpression(binary.Right, variables, diagnostics);
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
                
            default:
                diagnostics.Add(new AclDiagnostic(AclDiagnosticSeverity.Error, "Semantic", "Invalid expression type.", expr.Span));
                return null;
        }
    }
}
