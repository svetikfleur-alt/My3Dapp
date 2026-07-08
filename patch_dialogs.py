import re

# 1. ExtrudeFeatureDialog
with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\ExtrudeFeatureDialog.axaml.cs', 'r', encoding='utf-8') as f:
    ext_content = f.read()

ext_event = '''
    public event EventHandler<ExtrudeFeatureDialogResult>? PreviewRequested;

    private void OnInputsChanged()
    {
        var distance = (double)(DistanceInput.Value ?? 0m);
        var reverse = ReverseDirectionCheckBox.IsChecked == true;
        var signedDistance = reverse ? -distance : distance;
        var op = ResolveSelectedOperation();
        var targetId = ResolveTargetBodyId();
        PreviewRequested?.Invoke(this, new ExtrudeFeatureDialogResult(signedDistance, reverse, op, targetId));
    }
'''

ext_hook = '''
        DistanceInput.ValueChanged += (_, _) => OnInputsChanged();
        ReverseDirectionCheckBox.IsCheckedChanged += (_, _) => OnInputsChanged();
        TargetBodyComboBox.SelectionChanged += (_, _) => OnInputsChanged();
        UpdateOperationUi(CadExtrudeOperation.NewBody);
'''

ext_content = ext_content.replace('public double ConfirmedDistance { get; private set; }', ext_event.lstrip() + '    public double ConfirmedDistance { get; private set; }')
ext_content = ext_content.replace('UpdateOperationUi(CadExtrudeOperation.NewBody);', ext_hook.strip())

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\ExtrudeFeatureDialog.axaml.cs', 'w', encoding='utf-8') as f:
    f.write(ext_content)


# 2. HoleFeatureDialog
with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\HoleFeatureDialog.axaml.cs', 'r', encoding='utf-8') as f:
    hole_content = f.read()

hole_event = '''
    public event EventHandler<HoleFeatureDialogResult>? PreviewRequested;

    private void OnInputsChanged()
    {
        var diameter = (double)(DiameterInputControl.Value ?? 10m);
        var cx = (double)(CenterXInputControl.Value ?? 0m);
        var cy = (double)(CenterYInputControl.Value ?? 0m);
        var depthKind = BlindRadioControl.IsChecked == true ? "Blind" : "ThroughAll";
        var depthValue = (double)(DepthValueInputControl.Value ?? 20m);
        PreviewRequested?.Invoke(this, new HoleFeatureDialogResult(diameter, depthKind, depthValue, cx, cy));
    }
'''

hole_hook = '''
        DepthValueInputControl.IsEnabled = isBlind;
        DiameterInputControl.ValueChanged += (_, _) => OnInputsChanged();
        CenterXInputControl.ValueChanged += (_, _) => OnInputsChanged();
        CenterYInputControl.ValueChanged += (_, _) => OnInputsChanged();
        ThroughAllRadioControl.IsCheckedChanged += (_, _) => OnInputsChanged();
        BlindRadioControl.IsCheckedChanged += (_, _) => OnInputsChanged();
        DepthValueInputControl.ValueChanged += (_, _) => OnInputsChanged();
'''

hole_content = hole_content.replace('public double ConfirmedDiameter { get; private set; } = 10d;', hole_event.lstrip() + '    public double ConfirmedDiameter { get; private set; } = 10d;')
hole_content = hole_content.replace('DepthValueInputControl.IsEnabled = isBlind;', hole_hook.strip())

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\HoleFeatureDialog.axaml.cs', 'w', encoding='utf-8') as f:
    f.write(hole_content)


# 3. LinearPatternDialog
with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\LinearPatternDialog.axaml.cs', 'r', encoding='utf-8') as f:
    lp_content = f.read()

lp_event = '''
    public event EventHandler<LinearPatternDialogResult>? PreviewRequested;

    private void OnInputsChanged()
    {
        var count = Math.Max(2, (int)(CountInputControl.Value ?? 3m));
        var spacing = Math.Max(0.1d, (double)(SpacingInputControl.Value ?? 20m));
        var axis = (AxisComboBoxControl.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.Trim().ToLowerInvariant() ?? "x";
        PreviewRequested?.Invoke(this, new LinearPatternDialogResult(count, spacing, axis));
    }
'''

lp_hook = '''
        AxisComboBoxControl.SelectionChanged += (_, _) => OnInputsChanged();
        CountInputControl.ValueChanged += (_, _) => OnInputsChanged();
        SpacingInputControl.ValueChanged += (_, _) => OnInputsChanged();
    }
'''

lp_content = lp_content.replace('public int ConfirmedCount { get; private set; } = 3;', lp_event.lstrip() + '    public int ConfirmedCount { get; private set; } = 3;')
lp_content = lp_content.replace('        };\\n    }', '        };\n' + lp_hook)

with open(r'D:\My3DApp\My3DApp\AvaloniaApp\Dialogs\LinearPatternDialog.axaml.cs', 'w', encoding='utf-8') as f:
    f.write(lp_content)

