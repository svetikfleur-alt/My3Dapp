using System.Reflection;

var invocation = args.Length > 0
    ? string.Join(' ', args)
    : """
      let width = 120
      let height = 70

      if width >= 100 and height > 60
      {
        template mounting-plate width=$width height=$height thickness=5 holeCount=4
      }
      else
      {
        template mounting-plate width=$width height=50 thickness=4 holeCount=2
      }

      for size in [40, 60]
      {
        template fan-adapter fanSize=$size thickness=3 screwHoleDiameter=4.5 centerOpeningDiameter=${size - 18}
      }
      """;

var assemblyPath = ResolveAppAssemblyPath();
var assembly = Assembly.LoadFrom(assemblyPath);

var scriptType = assembly.GetType("My3DApp.AvaloniaApp.Services.CadScriptLibrary", throwOnError: true)!;
var parserType = assembly.GetType("My3DApp.AvaloniaApp.Services.CadCommandParser", throwOnError: true)!;

var tryExpand = scriptType.GetMethod("TryExpandSequence", BindingFlags.Public | BindingFlags.Static);
var tokenize = scriptType.GetMethod("TokenizeScript", BindingFlags.NonPublic | BindingFlags.Static);
string expanded;
string? aclError = null;
var expandedOk = false;

if (tryExpand is not null)
{
    object?[] invokeArgs = [invocation, null, null];
    expandedOk = (bool)(tryExpand.Invoke(null, invokeArgs) ?? false);
    expanded = invokeArgs[1] as string ?? string.Empty;
    aclError = invokeArgs[2] as string;
}
else
{
    var expand = scriptType.GetMethod("ExpandSequence", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("CadScriptLibrary.ExpandSequence not found.");
    expanded = expand.Invoke(null, [invocation]) as string ?? string.Empty;
    expandedOk = true;
}

var parser = Activator.CreateInstance(parserType)
    ?? throw new InvalidOperationException("Failed to create CadCommandParser.");
var parseSequence = parserType.GetMethod("ParseSequence", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new InvalidOperationException("CadCommandParser.ParseSequence not found.");
var steps = ((System.Collections.IEnumerable?)parseSequence.Invoke(parser, [invocation]))?.Cast<object>().ToArray()
    ?? [];

var failed = steps
    .Where(step =>
    {
        var result = step.GetType().GetProperty("Result")?.GetValue(step);
        return result?.GetType().GetProperty("IsSuccess")?.GetValue(result) as bool? != true;
    })
    .ToArray();

Console.WriteLine("ACL sample invocation:");
Console.WriteLine(invocation);
Console.WriteLine();
Console.WriteLine($"Using app assembly: {assemblyPath}");
Console.WriteLine();
if (tokenize is not null)
{
    var tokenList = ((System.Collections.IEnumerable?)tokenize.Invoke(null, [invocation]))?.Cast<object>().Select(item => item?.ToString() ?? string.Empty).ToArray()
        ?? [];
    Console.WriteLine($"Structured tokens: {tokenList.Length}");
    foreach (var token in tokenList.Take(16))
    {
        Console.WriteLine($"  TOK: {token}");
    }
    Console.WriteLine();
}
Console.WriteLine("Expanded command sequence:");
Console.WriteLine(expanded);
Console.WriteLine();
Console.WriteLine($"ACL structured expansion: {(expandedOk ? "OK" : "FALLBACK")}");
if (!expandedOk && !string.IsNullOrWhiteSpace(aclError))
{
    Console.WriteLine($"ACL error: {aclError}");
}
Console.WriteLine();
Console.WriteLine($"Parsed steps: {steps.Length}");
Console.WriteLine($"Failed steps: {failed.Length}");
Console.WriteLine();
Console.WriteLine("First parsed steps:");
foreach (var step in steps.Take(12))
{
    var stepType = step.GetType();
    var index = stepType.GetProperty("Index")?.GetValue(step);
    var text = stepType.GetProperty("Text")?.GetValue(step);
    var result = stepType.GetProperty("Result")?.GetValue(step);
    var isSuccess = result?.GetType().GetProperty("IsSuccess")?.GetValue(result) as bool? == true;
    var status = isSuccess ? "OK" : "FAIL";
    Console.WriteLine($"[{status}] {index}: {text}");
}

if (failed.Length > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var step in failed)
    {
        var stepType = step.GetType();
        var index = stepType.GetProperty("Index")?.GetValue(step);
        var result = stepType.GetProperty("Result")?.GetValue(step);
        var message = result?.GetType().GetProperty("Message")?.GetValue(result);
        Console.WriteLine($"Step {index}: {message}");
    }

    return 1;
}

Console.WriteLine();
Console.WriteLine("ACL smoke test passed.");
return 0;

static string ResolveAppAssemblyPath()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var repoMarker = Path.Combine(current.FullName, "My3DApp.csproj");
        var candidate = Path.Combine(current.FullName, "bin", "Debug", "net10.0-windows", "My3DApp.dll");
        if (File.Exists(repoMarker) && File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return Path.Combine(AppContext.BaseDirectory, "My3DApp.dll");
}
