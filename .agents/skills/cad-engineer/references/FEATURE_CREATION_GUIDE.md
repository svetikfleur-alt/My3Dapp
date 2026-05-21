# Feature Creation Guide

## Process Overview

Adding a new CAD feature to this application requires coordinated changes across the engine, view model, dialog, and backend layers. This guide walks through the complete process.

## Step 1: Define The Feature Enum

Add a new value to `CadFeatureKind`:

```csharp
// Engine/CadModel.cs
public enum CadFeatureKind
{
    // ... existing values ...
    MyNewFeature,  // <-- add here (alphabetical order within group)
}
```

If a new command action is needed, add to `CadCommandActionKind`:

```csharp
public enum CadCommandActionKind
{
    // ... existing values ...
    MyNewFeatureSelectedBody,  // <-- add here
}
```

## Step 2: Create The Feature Class

Add the feature class in `CadModel.cs`:

```csharp
[JsonDerivedType(typeof(MyNewFeature), typeDiscriminator: "mynewfeature")]
public sealed class MyNewFeature : CadFeature
{
    // Feature-specific parameters with sensible defaults
    public double SomeValue { get; set; } = 10d;

    public override CadFeatureKind Kind => CadFeatureKind.MyNewFeature;

    public override CadFeatureRole Role => CadFeatureRole.Derived; // or Operation, Derived, etc.

    public override IReadOnlyList<CadParameter> GetParameters() =>
    [
        CadParameter.Number("SomeValue", SomeValue),
    ];

    public override bool TrySetParameter(string key, double value)
    {
        switch (key)
        {
            case "SomeValue": SomeValue = value; return true;
            default: return false;
        }
    }
}
```

### Feature Class Requirements

- Sensible defaults (feature should work out of the box with no user input)
- All parameters exposed via `GetParameters()` for UI inspection
- `TrySetParameter()` supports editing from the property panel
- `JsonDerivedTypeAttribute` with a unique type discriminator
- `CadFeatureKind` enum value matching the class name convention

## Step 3: Add The Handler Method

In `CadProjectStore.cs`, add the handler:

```csharp
private CadActionResult HandleMyNewFeature(CadCommandAction action)
{
    // 1. Validate preconditions
    var body = ResolveSelectedBody();
    if (body is null)
        return Failure("Select a body first.");

    // Validate parameters
    if (action.Amount <= 0)
        return Failure("Amount must be positive.");

    // 2. Create and configure the feature
    var feature = new MyNewFeature
    {
        Name = $"MyFeature{NextIndex(CadFeatureKind.MyNewFeature)}",
        SomeValue = action.Amount,
    };

    // 3. Add to body's feature list
    body.Features.Add(feature);

    // 4. Update selection
    Project.Selection = new CadSelection(CadEntityKind.Body, body.Id, body.Name);

    // 5. Return success
    return Success($"Created {feature.Name}.", mutated: true);
}
```

### Handler Requirements

- Validate all preconditions at the top
- Return `Failure()` with a clear, user-facing message on validation failure
- Compute a unique name using `NextIndex()` or a counter
- Update `Project.Selection` if the feature changes what is selected
- Return `Success()` with `mutated: true` when state changes
- Catch nothing at the handler level — the `Apply()` catch-all handles exceptions

## Step 4: Wire The Handler

Add the handler to the `Apply()` switch expression:

```csharp
return action.Kind switch
{
    // ... existing cases ...
    CadCommandActionKind.MyNewFeatureSelectedBody => HandleMyNewFeature(action),
    // ...
};
```

## Step 5: Update The Compiler

If the feature produces geometry visible in the viewport or affects export, update `CadProjectCompiler`:

```csharp
// In the compiler's feature-processing loop
case MyNewFeature myFeat:
    // Translate feature state to Solid/Mesh or backend call
    break;
```

## Step 6: Create The Dialog (UI Layer)

In `AvaloniaApp/Dialogs/`:

1. Create `MyNewFeatureDialog.axaml` with parameter fields
2. Create `MyNewFeatureDialog.axaml.cs` with ViewModel bindings
3. Follow the `ToolDialogWindow` scaffold pattern
4. Use the dialog standards from `DIALOG_STANDARD.md`:
   - Title, purpose, primary action, cancel/close
   - Validation for required fields
   - Disable OK until valid
   - State update after successful action

## Step 7: Wire The Dialog

In the ViewModel layer, add:
1. A command that opens the dialog
2. Dialog result handling that dispatches the `CadCommandAction`
3. Error handling for failure results

## Step 8: Update Export (If Applicable)

If the feature produces new solid geometry:
1. Update `SolidCompiler` to translate the feature to `Solid` records
2. Verify `SolidMesher` produces valid mesh output
3. Test STL and OBJ export

## Step 9: Test

Test the feature end-to-end:
1. Create a body
2. Apply the feature via the dialog
3. Verify the viewport updates
4. Verify the tree shows the new feature
5. Verify undo reverts the feature
6. Save and reload the project — verify the feature persists
7. Export the body — verify the output includes the feature's geometry

## Checklist

- [ ] `CadFeatureKind` enum value added
- [ ] `CadCommandActionKind` enum value added (if new action type)
- [ ] Feature class created with `JsonDerivedTypeAttribute`
- [ ] Feature has sensible defaults
- [ ] `GetParameters()` and `TrySetParameter()` implemented
- [ ] Handler method added to `CadProjectStore.cs`
- [ ] Handler wired in `Apply()` switch
- [ ] Compiler updated (if geometry changes)
- [ ] Dialog created (UI layer)
- [ ] Dialog wired to ViewModel
- [ ] `dotnet build` passes with 0 errors, 0 warnings
- [ ] End-to-end flow verified
- [ ] Save/load roundtrip tested
