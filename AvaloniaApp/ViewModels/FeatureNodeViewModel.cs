using System.Collections.ObjectModel;
using FormaCore.Engine;

namespace My3DApp.AvaloniaApp.ViewModels;

public sealed class FeatureNodeViewModel
{
    public FeatureNodeViewModel(
        string name,
        string iconKind = "group",
        string typeLabel = "",
        string secondaryText = "",
        CadEntityKind entityKind = CadEntityKind.None,
        Guid entityId = default,
        bool isSelectable = false,
        bool isExpanded = false,
        bool isBodyVisible = true)
    {
        Name = name;
        IconKind = iconKind;
        TypeLabel = typeLabel;
        SecondaryText = secondaryText;
        EntityKind = entityKind;
        EntityId = entityId;
        IsSelectable = isSelectable;
        IsExpanded = isExpanded;
        IsBodyVisible = isBodyVisible;
    }

    public string Name { get; }

    public string IconKind { get; }

    public string TypeLabel { get; }

    public string SecondaryText { get; }

    public bool IsEditingSketch { get; init; }

    public bool CanStartSketchOnPlane => EntityKind == CadEntityKind.ReferencePlane;

    public bool CanEditSketch => EntityKind == CadEntityKind.Sketch && !IsEditingSketch;

    public bool CanDeleteSketchEntity => EntityKind == CadEntityKind.SketchEntity;

    public bool CanDeleteModelItem => EntityKind is CadEntityKind.Body or CadEntityKind.Feature or CadEntityKind.Sketch;

    public bool CanRenameItem => EntityKind == CadEntityKind.Body;

    public bool CanDuplicateItem => EntityKind == CadEntityKind.Body;

    public bool CanSetColor => EntityKind == CadEntityKind.Body;

    public bool CanToggleBodyVisibility => EntityKind == CadEntityKind.Body;

    public bool IsBodyVisible { get; }

    public double BodyOpacity => IsBodyVisible ? 1.0 : 0.42;

    public bool CanEditParameters => EntityKind is CadEntityKind.Body or CadEntityKind.Feature or CadEntityKind.SketchEntity;

    public CadEntityKind EntityKind { get; }

    public Guid EntityId { get; }

    public bool IsSelectable { get; }

    public bool IsExpanded { get; }

    public bool HasTypeLabel => !string.IsNullOrWhiteSpace(TypeLabel);

    public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);

    public bool HasIcon => IsSectionNode || !string.IsNullOrWhiteSpace(IconPath);

    public bool IsSectionNode => IconKind is "reference-group" or "sketch-group" or "feature-group" or "part-group";

    public bool ShowGlyph => HasIcon && !IsSectionNode;

    public bool ShowSectionName => IsSectionNode;

    public bool ShowItemName => !IsSectionNode;

    public bool ShowSecondaryText => HasSecondaryText && !IsSectionNode;

    public bool HasSvgIcon => !string.IsNullOrWhiteSpace(IconPath) && !IsSectionNode;

    private const string IconBase = "avares://My3DApp/Assets/Icons/";

    public string IconPath => IconKind switch
    {
        "body" or "part"          => IconBase + "box.svg",
        "plane" or "datum-plane"  => IconBase + "sketch-plane.svg",
        "origin"                  => IconBase + "point.svg",
        "sketch"                  => IconBase + "pencil.svg",
        "extrude"                 => IconBase + "extrude.svg",
        "revolve"                 => IconBase + "revolve.svg",
        "sweep" or "loft"         => IconBase + "extrude.svg",
        "fillet"                  => IconBase + "fillet.svg",
        "chamfer"                 => IconBase + "chamfer.svg",
        "shell"                   => IconBase + "shell.svg",
        "mirror"                  => IconBase + "mirror.svg",
        "linear-pattern"          => IconBase + "linear-pattern.svg",
        "circular-pattern"        => IconBase + "circular-pattern.svg",
        "hole"                    => IconBase + "hole.svg",
        "move"                    => IconBase + "move.svg",
        "boolean"                 => IconBase + "boolean.svg",
        "box"                     => IconBase + "box.svg",
        "cylinder"                => IconBase + "cylinder.svg",
        "sphere"                  => IconBase + "sphere.svg",
        "cone"                    => IconBase + "cone.svg",
        "torus"                   => IconBase + "torus.svg",
        "prism"                   => IconBase + "prism.svg",
        "pyramid"                 => IconBase + "pyramid.svg",
        "hemisphere"              => IconBase + "hemisphere.svg",
        "capsule"                 => IconBase + "capsule.svg",
        "icosphere"               => IconBase + "icosphere.svg",
        "wedge"                   => IconBase + "wedge.svg",
        "arrow-primitive"         => IconBase + "arrow-primitive.svg",
        "tetrahedron"             => IconBase + "tetrahedron.svg",
        "octahedron"              => IconBase + "octahedron.svg",
        "icosahedron"             => IconBase + "icosahedron.svg",
        "ellipsoid"               => IconBase + "ellipsoid.svg",
        "feature"                 => IconBase + "extrude.svg",
        "sketch-entity"           => IconBase + "line.svg",
        "constraint"              => IconBase + "point.svg",
        "dimension"               => IconBase + "line.svg",
        _ => string.Empty
    };

    public string IconToken => IconKind switch
    {
        "group"            => "GR",
        "reference-group"  => "REF",
        "sketch-group"     => "SK",
        "feature-group"    => "FX",
        "part-group"       => "PRT",
        "body" or "part"   => "BD",
        "plane" or "datum-plane" => "PL",
        "origin"           => "OR",
        "sketch"           => "SK",
        "extrude"          => "EX",
        "revolve"          => "RV",
        "sweep"            => "SW",
        "loft"             => "LF",
        "fillet"           => "FL",
        "chamfer"          => "CH",
        "shell"            => "SH",
        "mirror"           => "MI",
        "linear-pattern"   => "LP",
        "circular-pattern" => "CP",
        "hole"             => "HO",
        "move"             => "MV",
        "boolean"          => "BO",
        "feature"          => "Fx",
        "sketch-entity"    => "GE",
        "constraint"       => "CS",
        "dimension"        => "DM",
        _ => string.Empty
    };

    public ObservableCollection<FeatureNodeViewModel> Children { get; } = [];
}
