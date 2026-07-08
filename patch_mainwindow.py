import re

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\MainWindow.axaml.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Extrude new feature
content = re.sub(
    r'var dialog = new ExtrudeFeatureDialog\(([^;]+)\);\s*var result = await ShowAnchoredDialogAsync<ExtrudeFeatureDialogResult\?>\(dialog\);',
    r'var dialog = new ExtrudeFeatureDialog(\1);\n' +
    r'        dialog.PreviewRequested += (s, args) =>\n' +
    r'        {\n' +
    r'            var graph = _wiredViewModel.PreviewExtrude(args.Distance, args.Operation, args.TargetBodyId);\n' +
    r'            if (graph != null) _exactScene?.DisplayFeatureGraph(graph);\n' +
    r'        };\n' +
    r'        var result = await ShowAnchoredDialogAsync<ExtrudeFeatureDialogResult?>(dialog);\n' +
    r'        if (result == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);',
    content
)

# Hole new feature
content = re.sub(
    r'var dialog = new HoleFeatureDialog\(([^;]+)\);\s*var result = await ShowAnchoredDialogAsync<HoleFeatureDialogResult\?>\(dialog\);',
    r'var dialog = new HoleFeatureDialog(\1);\n' +
    r'            dialog.PreviewRequested += (s, args) =>\n' +
    r'            {\n' +
    r'                var graph = _wiredViewModel.PreviewHole(args.Diameter, args.CenterOffsetX, args.CenterOffsetY, args.DepthKind == "Blind" ? $"Blind:{args.DepthValue}" : "ThroughAll");\n' +
    r'                if (graph != null) _exactScene?.DisplayFeatureGraph(graph);\n' +
    r'            };\n' +
    r'            var result = await ShowAnchoredDialogAsync<HoleFeatureDialogResult?>(dialog);\n' +
    r'            if (result == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);',
    content
)

# LinearPattern new feature
content = re.sub(
    r'var lpDialog = new LinearPatternDialog\(([^;]+)\);\s*var lpResult = await ShowAnchoredDialogAsync<LinearPatternDialogResult\?>\(lpDialog\);',
    r'var lpDialog = new LinearPatternDialog(\1);\n' +
    r'                lpDialog.PreviewRequested += (s, args) =>\n' +
    r'                {\n' +
    r'                    var graph = _wiredViewModel.PreviewLinearPattern(args.Count, args.Spacing, args.Axis);\n' +
    r'                    if (graph != null) _exactScene?.DisplayFeatureGraph(graph);\n' +
    r'                };\n' +
    r'                var lpResult = await ShowAnchoredDialogAsync<LinearPatternDialogResult?>(lpDialog);\n' +
    r'                if (lpResult == null && _wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph != null) _exactScene?.DisplayFeatureGraph(_wiredViewModel.AclWorkspace.BuildCoordinator.Session.CurrentGraph);',
    content
)

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\MainWindow.axaml.cs', 'w', encoding='utf-8') as f:
    f.write(content)

