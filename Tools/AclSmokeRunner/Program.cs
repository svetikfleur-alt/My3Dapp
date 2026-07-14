using My3DApp.AvaloniaApp.Services;

// Headless smoke test for the MVP end-to-end flow. Exercises the real pipeline
// (ACL expansion -> parse -> StudioWorkspaceController -> CadProjectStore ->
// compile -> viewport payload -> STL/OBJ export) without any UI.
// Exit code 0 = all stages passed.

var invocation = args.Length > 0
    ? string.Join(' ', args)
    : "template controller-box-kit boxWidth=140 boxDepth=95 boxHeight=48 wallThickness=3 lidThickness=3 standoffHeight=10 cableDiameter=7 fanSize=80";

var failures = 0;

void Check(bool condition, string label)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {label}");
    if (!condition)
    {
        failures++;
    }
}

// ---- Stage 1: ACL expansion --------------------------------------------
Console.WriteLine("=== Stage 1: ACL expansion ===");
Console.WriteLine($"Invocation: {invocation}");
var expandedOk = CadScriptLibrary.TryExpandSequence(invocation, out var expanded, out var aclError);
Check(expandedOk, "ACL structured expansion succeeds");
if (!expandedOk)
{
    Console.WriteLine($"ACL error: {aclError}");
}
else
{
    Console.WriteLine($"Expanded: {expanded}");
}

// ---- Stage 2: command parsing ------------------------------------------
Console.WriteLine();
Console.WriteLine("=== Stage 2: command parsing ===");
var parser = new CadCommandParser();
var steps = parser.ParseSequence(invocation);
var failedSteps = steps.Where(step => !step.Result.IsSuccess || step.Result.Commands.Count == 0).ToArray();
Console.WriteLine($"Parsed steps: {steps.Count}, failed: {failedSteps.Length}");
foreach (var step in failedSteps)
{
    Console.WriteLine($"  Step {step.Index}: '{step.Text}' -> {step.Result.Message}");
}
Check(steps.Count > 0 && failedSteps.Length == 0, "all parsed steps succeed");

// ---- Stage 3: end-to-end execution, state, and export -------------------
Console.WriteLine();
Console.WriteLine("=== Stage 3: headless end-to-end (sample box) ===");
var controller = new StudioWorkspaceController();
Check(controller.CurrentState.CompileResult.Bodies.Count == 0, "new workspace starts with zero bodies");

var boxExecuted = ExecuteSequence(controller, parser, "create box 40x30x20");
Check(boxExecuted, "'create box 40x30x20' executes through the command pipeline");

var state = controller.CurrentState;
Check(state.CompileResult.Bodies.Count == 1, $"compiled body count is 1 (actual {state.CompileResult.Bodies.Count})");
Check(state.Project.Scene.Bodies.Count == 1, "scene tree contains the body");
Check(state.Project.Scene.Bodies.Sum(b => b.Features.Count) > 0, "body carries feature history entries");

var renderBody = state.ViewportState.Bodies.FirstOrDefault();
Check(renderBody is not null, "viewport render state contains the body");
Check(renderBody is { Positions.Length: > 0, Indices.Length: > 0 },
    $"viewport body has real mesh data (positions {renderBody?.Positions.Length ?? 0}, indices {renderBody?.Indices.Length ?? 0})");
Check(controller.CanUndo, "undo history recorded the mutation");

var stlPath = Path.Combine(Path.GetTempPath(), $"acl-smoke-{Guid.NewGuid():N}.stl");
controller.ExportStl(stlPath);
var stlText = File.Exists(stlPath) ? File.ReadAllText(stlPath) : string.Empty;
Check(stlText.Contains("facet normal") && stlText.Contains("vertex"), $"STL export contains real triangles ({stlText.Length} bytes)");
File.Delete(stlPath);

Console.WriteLine();
Console.WriteLine("=== Stage 4: headless end-to-end (sample ACL script) ===");
controller.NewProject();
Check(controller.CurrentState.CompileResult.Bodies.Count == 0, "new project resets bodies");
Console.WriteLine("Script:");
Console.WriteLine(CadScriptLibrary.SampleAclScript);
var aclExecuted = ExecuteSequence(controller, parser, CadScriptLibrary.SampleAclScript);
Check(aclExecuted, "sample ACL script executes through the command pipeline");

state = controller.CurrentState;
Check(state.CompileResult.Bodies.Count >= 1, $"sample ACL produced a compiled body (actual {state.CompileResult.Bodies.Count})");
Check(state.ViewportState.Bodies.Any(b => b.Positions.Length > 0 && b.Indices.Length > 0), "sample ACL body has real viewport mesh data");

var objPath = Path.Combine(Path.GetTempPath(), $"acl-smoke-{Guid.NewGuid():N}.obj");
controller.ExportObj(objPath);
var objText = File.Exists(objPath) ? File.ReadAllText(objPath) : string.Empty;
Check(objText.Contains("v ") && objText.Contains("f "), $"OBJ export contains vertices and faces ({objText.Length} bytes)");
File.Delete(objPath);

Console.WriteLine();
if (failures > 0)
{
    Console.WriteLine($"Smoke test FAILED: {failures} check(s) failed.");
    return 1;
}

Console.WriteLine("All smoke stages passed.");
return 0;

static bool ExecuteSequence(StudioWorkspaceController controller, CadCommandParser parser, string script)
{
    foreach (var step in parser.ParseSequence(script))
    {
        if (!step.Result.IsSuccess || step.Result.Commands.Count == 0)
        {
            Console.WriteLine($"  parse failure at step {step.Index}: '{step.Text}' -> {step.Result.Message}");
            return false;
        }

        foreach (var command in step.Result.Commands)
        {
            var result = controller.ExecuteCommand(command);
            if (!result.Success)
            {
                Console.WriteLine($"  execution failure at step {step.Index}: {command.Describe()} -> {result.Message}");
                return false;
            }
        }
    }

    return true;
}
