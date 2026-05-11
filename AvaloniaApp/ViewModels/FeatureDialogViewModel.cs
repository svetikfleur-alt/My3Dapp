namespace My3DApp.AvaloniaApp.ViewModels;

/// <summary>Result returned from the feature creation dialog.</summary>
public class FeatureDialogResult
{
    public bool   Confirmed      { get; init; }
    public string Mode           { get; init; } = "New";
    public float  Depth          { get; init; } = 50f;
    public float  DraftAngle     { get; init; } = 0f;
    public float  Angle          { get; init; } = 360f;
    public float  PathLength     { get; init; } = 80f;
    public float  TwistAngle     { get; init; } = 0f;
    public float  LoftHeight     { get; init; } = 60f;
    public float  LoftScale      { get; init; } = 0.6f;
    public float  FilletRadius   { get; init; } = 3f;
    public float  ChamferDist    { get; init; } = 2f;
    public float  ChamferAngle   { get; init; } = 45f;
}

/// <summary>View-model backing the feature creation dialog.</summary>
public class FeatureDialogViewModel : ViewModelBase
{
    public string FeatureType { get; }
    public string Title       { get; }

    // Visibility flags for optional sections
    public bool ShowModes       { get; }
    public bool ShowDepth       { get; }
    public bool ShowDraftAngle  { get; }
    public bool ShowAngle       { get; }
    public bool ShowPathLength  { get; }
    public bool ShowTwistAngle  { get; }
    public bool ShowLoftOptions { get; }
    public bool ShowFillet      { get; }
    public bool ShowChamfer     { get; }

    public string[] AllModes { get; } = ["New", "Add", "Remove", "Surface", "Thin"];

    // Selected mode index so ListBox / RadioButton group can bind by index
    private int _modeIndex = 0;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            if (SetField(ref _modeIndex, value))
                OnPropertyChanged(nameof(SelectedMode));
        }
    }
    public string SelectedMode => ModeIndex >= 0 && ModeIndex < AllModes.Length
        ? AllModes[ModeIndex] : "New";

    // String-typed dimension fields — code-behind parses on OK
    private string _depth = "50";
    public string Depth { get => _depth; set => SetField(ref _depth, value); }

    private string _draftAngle = "0";
    public string DraftAngle { get => _draftAngle; set => SetField(ref _draftAngle, value); }

    private string _angle = "360";
    public string Angle { get => _angle; set => SetField(ref _angle, value); }

    private string _pathLength = "80";
    public string PathLength { get => _pathLength; set => SetField(ref _pathLength, value); }

    private string _twistAngle = "0";
    public string TwistAngle { get => _twistAngle; set => SetField(ref _twistAngle, value); }

    private string _loftHeight = "60";
    public string LoftHeight { get => _loftHeight; set => SetField(ref _loftHeight, value); }

    private string _loftScale = "0.6";
    public string LoftScale { get => _loftScale; set => SetField(ref _loftScale, value); }

    private string _filletRadius = "3";
    public string FilletRadius { get => _filletRadius; set => SetField(ref _filletRadius, value); }

    private string _chamferDist = "2";
    public string ChamferDist { get => _chamferDist; set => SetField(ref _chamferDist, value); }

    private string _chamferAngle = "45";
    public string ChamferAngle { get => _chamferAngle; set => SetField(ref _chamferAngle, value); }

    public FeatureDialogViewModel(string featureType)
    {
        FeatureType = featureType;
        Title = featureType switch
        {
            "extrude"  => "Extrude",
            "revolve"  => "Revolve",
            "loft"     => "Loft",
            "sweep"    => "Sweep",
            "fillet"   => "Fillet",
            "chamfer"  => "Chamfer",
            _          => featureType
        };

        ShowModes       = featureType is "extrude" or "revolve" or "loft" or "sweep";
        ShowDepth       = featureType is "extrude";
        ShowDraftAngle  = featureType is "extrude" or "loft";
        ShowAngle       = featureType is "revolve";
        ShowPathLength  = featureType is "sweep";
        ShowTwistAngle  = featureType is "sweep";
        ShowLoftOptions = featureType is "loft";
        ShowFillet      = featureType is "fillet";
        ShowChamfer     = featureType is "chamfer";
    }

    private static float Parse(string s, float fallback = 0f)
        => float.TryParse(s, System.Globalization.NumberStyles.Float,
               System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public FeatureDialogResult ToResult(bool confirmed) => new()
    {
        Confirmed    = confirmed,
        Mode         = SelectedMode,
        Depth        = Parse(Depth, 50f),
        DraftAngle   = Parse(DraftAngle, 0f),
        Angle        = Parse(Angle, 360f),
        PathLength   = Parse(PathLength, 80f),
        TwistAngle   = Parse(TwistAngle, 0f),
        LoftHeight   = Parse(LoftHeight, 60f),
        LoftScale    = Parse(LoftScale, 0.6f),
        FilletRadius = Parse(FilletRadius, 3f),
        ChamferDist  = Parse(ChamferDist, 2f),
        ChamferAngle = Parse(ChamferAngle, 45f),
    };
}
