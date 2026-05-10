using My3DApp.AvaloniaApp.Services;
using My3DApp.AvaloniaApp.ViewModels;
using FormaCore.Engine;

var vm = new StudioShellViewModel();

await vm.SelectTreeNodeAsync(vm.FeatureNodes.SelectMany(Flatten).First(node => node.Name == "Top"));
await vm.StartSketchAsync();
await vm.SelectSketchToolAsync("Rectangle");
await vm.HandleViewportSketchPlacementAsync(0, 0);
await vm.HandleViewportSketchPlacementAsync(18, 10);
await vm.FinishSketchAsync();
await vm.ExtrudeSelectedSketchAsync(12);

Console.WriteLine($"EXTRUDE_CAN={vm.CanProfileTools}");
Console.WriteLine($"PROFILE_SUMMARY={vm.SelectedProfileSummary}");
Console.WriteLine($"TREE_HAS_PART={vm.FeatureNodes.SelectMany(Flatten).Any(node => node.Name == \"Part1\")}");
Console.WriteLine($"TREE_HAS_EXTRUDE={vm.FeatureNodes.SelectMany(Flatten).Any(node => node.Name == \"Extrude1\")}");

vm = new StudioShellViewModel();
await vm.SelectTreeNodeAsync(vm.FeatureNodes.SelectMany(Flatten).First(node => node.Name == "Top"));
await vm.StartSketchAsync();
await vm.SelectSketchToolAsync("Line");
await vm.HandleViewportSketchPlacementAsync(0, 0);
await vm.HandleViewportSketchPlacementAsync(0, 12);
await vm.HandleViewportSketchPlacementAsync(8, 12);
await vm.HandleViewportSketchPlacementAsync(8, 0);
await vm.HandleViewportSketchPlacementAsync(0, 0);
await vm.FinishSketchAsync();
await vm.RevolveSelectedSketchAsync(270, "y");

Console.WriteLine($"REVOLVE_SUMMARY={vm.SelectedProfileSummary}");
Console.WriteLine($"TREE_HAS_REVOLVE={vm.FeatureNodes.SelectMany(Flatten).Any(node => node.Name == \"Revolve1\")}");
Console.WriteLine($"TREE_HAS_PART_AFTER_REVOLVE={vm.FeatureNodes.SelectMany(Flatten).Any(node => node.Name == \"Part1\")}");

static IEnumerable<FeatureNodeViewModel> Flatten(FeatureNodeViewModel node)
{
    yield return node;
    foreach (var child in node.Children.SelectMany(Flatten))
    {
        yield return child;
    }
}
