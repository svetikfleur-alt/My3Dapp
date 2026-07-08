import re

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\ViewModels\StudioShellViewModel.cs', 'r', encoding='utf-8') as f:
    content = f.read()

acl_service_field = "    private readonly FormaCore.Engine.Acl.IAclSourceEditService _aclEditService = new FormaCore.Engine.Acl.AclSourceEditService();\n"
get_selected_method = '''
    private string GetSelectedFeatureName()
    {
        return FeatureNodes.FirstOrDefault(n => n.IsSelected)?.Name ?? "sketch1";
    }

    public FormaCore.Engine.Exact.Graph.ExactFeatureGraph? PreviewExtrude(double distance, FormaCore.Engine.CadExtrudeOperation operation, Guid targetBodyId)
    {
        var sketchName = GetSelectedFeatureName();
        var featureCode = $"extrude(profile: {sketchName}, depth: {distance}mm);";
        var tempSource = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        return AclWorkspace.ComputePreview(tempSource);
    }

    public FormaCore.Engine.Exact.Graph.ExactFeatureGraph? PreviewHole(double diameter, double centerOffsetX, double centerOffsetY, string depthInfo)
    {
        var targetName = GetSelectedFeatureName();
        var depth = depthInfo.StartsWith("Blind:") ? depthInfo.Substring(6) + "mm" : "0mm";
        var featureCode = $"hole(target: {targetName}, diameter: {diameter}mm, depth: {depth}, x: {centerOffsetX}mm, y: {centerOffsetY}mm);";
        var tempSource = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        return AclWorkspace.ComputePreview(tempSource);
    }

    public FormaCore.Engine.Exact.Graph.ExactFeatureGraph? PreviewLinearPattern(int count, double spacing, string axis)
    {
        var sourceName = GetSelectedFeatureName();
        double dx = axis == "x" ? 1 : 0;
        double dy = axis == "y" ? 1 : 0;
        double dz = axis == "z" ? 1 : 0;
        var featureCode = $"linear_pattern(source: {sourceName}, count: {count}, spacing: {spacing}mm, dx: {dx}, dy: {dy}, dz: {dz});";
        var tempSource = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        return AclWorkspace.ComputePreview(tempSource);
    }
'''

content = content.replace("public AclWorkspaceViewModel AclWorkspace { get; }", "public AclWorkspaceViewModel AclWorkspace { get; }\n" + acl_service_field + get_selected_method)

extrude_new = '''
    public async Task ExtrudeSelectedSketchAsync(double distance, CadExtrudeOperation operation, Guid targetBodyId, CancellationToken cancellationToken = default)
    {
        var sketchName = GetSelectedFeatureName();
        var featureCode = $"extrude(profile: {sketchName}, depth: {distance}mm);";
        AclWorkspace.SourceText = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        AclWorkspace.Build();
        await Task.CompletedTask;
    }
'''

content = re.sub(r'public async Task ExtrudeSelectedSketchAsync\(double distance, CadExtrudeOperation operation, Guid targetBodyId, CancellationToken cancellationToken = default\)\s*\{[^}]+}', extrude_new.strip(), content)

hole_new = '''
    public async Task HoleSelectedBodyAsync(double diameter = 10d, double centerOffsetX = 0d, double centerOffsetY = 0d, string depthInfo = "ThroughAll", CancellationToken cancellationToken = default)
    {
        var targetName = GetSelectedFeatureName();
        var depth = depthInfo.StartsWith("Blind:") ? depthInfo.Substring(6) + "mm" : "0mm";
        var featureCode = $"hole(target: {targetName}, diameter: {diameter}mm, depth: {depth}, x: {centerOffsetX}mm, y: {centerOffsetY}mm);";
        AclWorkspace.SourceText = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        AclWorkspace.Build();
        await Task.CompletedTask;
    }
'''

content = re.sub(r'public async Task HoleSelectedBodyAsync\(double diameter = 10d, double centerOffsetX = 0d, double centerOffsetY = 0d, string depthInfo = "ThroughAll", CancellationToken cancellationToken = default\)\s*\{[^}]+}', hole_new.strip(), content)

lp_new = '''
    public async Task LinearPatternSelectedBodyAsync(int count = 3, double spacing = 20d, string axis = "x", CancellationToken cancellationToken = default)
    {
        var sourceName = GetSelectedFeatureName();
        double dx = axis == "x" ? 1 : 0;
        double dy = axis == "y" ? 1 : 0;
        double dz = axis == "z" ? 1 : 0;
        var featureCode = $"linear_pattern(source: {sourceName}, count: {count}, spacing: {spacing}mm, dx: {dx}, dy: {dy}, dz: {dz});";
        AclWorkspace.SourceText = _aclEditService.AppendFeatureToPart(AclWorkspace.SourceText, "plate", featureCode);
        AclWorkspace.Build();
        await Task.CompletedTask;
    }
'''

content = re.sub(r'public async Task LinearPatternSelectedBodyAsync\(int count = 3, double spacing = 20d, string axis = "x", CancellationToken cancellationToken = default\)\s*\{[^}]+}', lp_new.strip(), content)

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\ViewModels\StudioShellViewModel.cs', 'w', encoding='utf-8') as f:
    f.write(content)

