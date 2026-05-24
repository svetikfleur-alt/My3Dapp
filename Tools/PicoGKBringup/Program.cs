using FormaCore.Engine;

var outputPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "output", "picogk_enclosure_demo.stl"));

var outputDirectory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrWhiteSpace(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

PicoGkExperimentalGeometryKernelAdapter.RunInSession(
    0.5f,
    adapter =>
    {
        var enclosure = adapter.CreateBox(92, 66, 28);
        var cavity = adapter.Translate(adapter.CreateBox(80, 54, 24), 0, 0, 5);
        var standoffA = adapter.Translate(adapter.CreateCylinder(3.6, 28), -28, -16, 0);
        var standoffB = adapter.Translate(adapter.CreateCylinder(3.6, 28), 28, -16, 0);
        var ventCut = adapter.Translate(adapter.CreateCylinder(6, 28), 0, 18, 0);

        var shell = adapter.Subtract(enclosure, cavity);
        var withStandoffA = adapter.Subtract(shell, standoffA);
        var withStandoffB = adapter.Subtract(withStandoffA, standoffB);
        var finalBody = adapter.Subtract(withStandoffB, ventCut);

        adapter.ExportStl(finalBody, outputPath);
        var mesh = adapter.Tessellate(finalBody);

        Console.WriteLine("PicoGK bring-up demo finished.");
        Console.WriteLine($"Adapter       : {adapter.DisplayName}");
        Console.WriteLine("Session mode  : PicoGK.Library.Go");
        Console.WriteLine("Voxel size mm : 0.5");
        Console.WriteLine($"Output        : {outputPath}");
        Console.WriteLine($"Vertices      : {mesh.Vertices.Count}");
        Console.WriteLine($"Triangles     : {mesh.Triangles.Count}");
    });
