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
        var nodeMap = new Dictionary<string, ExactFeatureNode>();

        foreach (var node in document.Nodes)
        {
            if (node is AclSketchDeclaration sketch)
            {
                foreach (var stmt in sketch.Statements)
                {
                    if (stmt is AclExpressionStatement exprStmt && exprStmt.Expression is AclCallExpression call)
                    {
                        var featureId = $"{sketch.Name}_feature_{featureIndex++}";
                        var featureNode = CreateFeatureNode(featureId, call, validationResult.ParameterValues, nodeMap);
                        if (featureNode != null)
                        {
                            graph.AddNode(featureNode);
                            nodeMap[sketch.Name] = featureNode; // simplistic: sketch name maps to the last shape in it
                        }
                    }
                }
            }
            else if (node is AclLetDeclaration letDecl && letDecl.Value is AclCallExpression letCall)
            {
                var featureId = letDecl.Name;
                var featureNode = CreateFeatureNode(featureId, letCall, validationResult.ParameterValues, nodeMap);
                if (featureNode != null)
                {
                    graph.AddNode(featureNode);
                    nodeMap[letDecl.Name] = featureNode;
                }
            }
            else if (node is AclPartDeclaration part)
            {
                foreach (var stmt in part.Statements)
                {
                    if (stmt is AclExpressionStatement exprStmt && exprStmt.Expression is AclCallExpression call)
                    {
                        var featureId = $"{part.Name}_feature_{featureIndex++}";
                        var featureNode = CreateFeatureNode(featureId, call, validationResult.ParameterValues, nodeMap);
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

    private ExactFeatureNode? CreateFeatureNode(string id, AclCallExpression call, IReadOnlyDictionary<string, double> parameters, Dictionary<string, ExactFeatureNode> nodeMap)
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
        else if (call.FunctionName == "rectangle")
        {
            var width = GetArgumentValue(call, "width", parameters) ?? 10.0;
            var height = GetArgumentValue(call, "height", parameters) ?? 10.0;
            return new ExactRectangleNode(id, call.Span, width, height);
        }
        else if (call.FunctionName == "circle")
        {
            var radius = GetArgumentValue(call, "radius", parameters) ?? 5.0;
            return new ExactCircleNode(id, call.Span, radius);
        }
        else if (call.FunctionName == "extrude")
        {
            var profileName = GetArgumentIdentifier(call, "profile");
            var depth = GetArgumentValue(call, "depth", parameters) ?? 10.0;
            if (profileName != null && nodeMap.TryGetValue(profileName, out var profileNode))
            {
                return new ExactExtrudeNode(id, call.Span, profileNode, depth);
            }
        }
        else if (call.FunctionName == "hole")
        {
            var targetName = GetArgumentIdentifier(call, "target");
            var diameter = GetArgumentValue(call, "diameter", parameters) ?? 10.0;
            var depth = GetArgumentValue(call, "depth", parameters) ?? 0.0; // 0 for through_all
            // simplistic position extraction (since user asked for position: [30mm, 20mm])
            // If we don't have vector parsing yet, we'll assume 0,0 for now.
            var posX = GetArgumentValue(call, "x", parameters) ?? 0.0;
            var posY = GetArgumentValue(call, "y", parameters) ?? 0.0;
            
            if (targetName != null && nodeMap.TryGetValue(targetName, out var targetNode))
            {
                return new ExactHoleNode(id, call.Span, targetNode, diameter, posX, posY, depth);
            }
        }
        else if (call.FunctionName == "linear_pattern")
        {
            var sourceName = GetArgumentIdentifier(call, "source");
            var count = (int)(GetArgumentValue(call, "count", parameters) ?? 2.0);
            var spacing = GetArgumentValue(call, "spacing", parameters) ?? 10.0;
            // simplistic direction
            var dx = GetArgumentValue(call, "dx", parameters) ?? 1.0;
            var dy = GetArgumentValue(call, "dy", parameters) ?? 0.0;
            var dz = GetArgumentValue(call, "dz", parameters) ?? 0.0;

            if (sourceName != null && nodeMap.TryGetValue(sourceName, out var sourceNode))
            {
                return new ExactLinearPatternNode(id, call.Span, sourceNode, dx, dy, dz, count, spacing);
            }
        }

        return null;
    }

    private string? GetArgumentIdentifier(AclCallExpression call, string argName)
    {
        var arg = call.Arguments.FirstOrDefault(a => a.Name == argName);
        if (arg?.Expression is AclIdentifierExpression id) return id.Name;
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
