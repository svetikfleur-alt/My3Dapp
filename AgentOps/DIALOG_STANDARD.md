# AgentOps dialog standard

Every feature operator (Extrude, Revolve, Fillet, Sweep, Loft, Boolean, Pattern, Hole, etc.) and every parametric sketch tool (Polygon, Offset, Fillet2D, etc.) opens a dialog. This document defines what a "normal" dialog looks like in this app — same level of polish as Onshape's feature dialogs.

## Where it lives

- All feature / tool dialogs use the `ToolDialogWindow` scaffold (`AvaloniaApp/Dialogs/ToolDialogWindow.axaml`).
- The body of each dialog is a `UserControl` named `<Feature>FeatureDialog.axaml(.cs)` (e.g. `RevolveFeatureDialog`, `SweepFeatureDialog`) or `<Tool>SketchToolOptionsView.axaml(.cs)`.
- Dialogs are reusable: the same scaffold renders Cancel/OK and handles Esc/Enter.

## Layout rules

The dialog body is divided into compact, labeled sections (cards). Each card is:
- A short title in the header style (`FeatureDialogSectionHeader` style).
- A vertical stack of fields below the title.
- Padding consistent with existing `FeatureDialog*` styles (don't invent new spacing).

Field rows are:
- A label on the left (fixed width, right-aligned text).
- The control on the right (input or selection picker).
- Vertical spacing of 8–10 px between rows. No more.

## Required sections per dialog type

**Feature operator (3D)**:
1. **Source** — what's being operated on (sketch / face / body / feature). Use the standard `SelectionPicker` control if the codebase has one; otherwise a clear "Pick:" button + read-only label showing current selection.
2. **Parameters** — the parametric inputs (angle, distance, count, radius, etc.). Use proper typed controls: `NumericUpDown` for numbers, `ComboBox` for enums, `CheckBox` for booleans. Never a raw `TextBox` for a number.
3. **Boolean op** (where applicable) — segmented control: New body / Join / Cut / Intersect.
4. **End conditions** (Extrude/Sweep/Loft) — segmented control with the available options.

**Sketch tool with options (Polygon, Offset, Fillet2D, etc.)**:
1. **Tool / Mode** — small descriptive header.
2. **Options** — the parameter inputs with proper typed controls.

## Controls — pick the right type

- Numbers (distance, radius, angle, count): `NumericUpDown` with sensible Min / Max / Increment. Show the unit (mm, deg, count) in a suffix label, NOT inside the editable field.
- Enums (Boolean op, End condition, Direction): segmented `ToggleButton`s in a horizontal `StackPanel`, OR a `ComboBox` if there are >4 options.
- Booleans (Reverse direction, Symmetric, Tangent extension): `CheckBox` with label.
- Selection (Profile, Path, Edges, Faces): a `Button` "Pick" + a read-only `TextBlock` summary like "1 sketch profile" or "3 edges".

## Validation

- Dialogs must not let the user click OK with invalid params.
- Disable OK button (`IsEnabled = false`) until all required fields are valid.
- Inline error text below offending field, in the error-text style. No popups.
- Provide sensible defaults so dialog opens with valid values out of the box (e.g. Extrude distance default = 10 mm; Polygon n = 6).

## Buttons + behavior

- Cancel on the LEFT, OK on the RIGHT (consistent with Windows convention).
- OK button labeled appropriately for the action: "Create" for new features, "Apply" for edits.
- Esc = Cancel, Enter = OK (handled by ToolDialogWindow scaffold — don't override).
- After OK: dialog closes, viewport reflects the change, feature appears in tree.
- After Cancel: dialog closes, no state changes.

## Visual polish

- Theme parity Light/Dark — every control must have proper styles in both `Studio.Light.axaml` and `Studio.Dark.axaml`. If you add a new control style, add it to BOTH theme files in the same commit.
- Corner radius and drop shadow are inherited from ToolDialogWindow. Don't customize per-dialog.
- Header / body / footer separator lines: use the existing dialog styles, not new ones.
- Width: 380–460 px for parameter dialogs. Don't go wider unless absolutely necessary.

## What NOT to do

- No raw `TextBox` for a number.
- No "Unnamed parameter" or untranslated `nameof(...)` in labels.
- No empty placeholder dialogs (a dialog with no parametric fields is a sign the feature isn't really a feature).
- No bespoke dialog sizing per feature — use the scaffold defaults.
- No "Apply / Apply and Close" double buttons. One OK, one Cancel.
- No SaaS-style cards with big shadows / colored headers / icons everywhere.

---

Reference: `References/onshape/blocks/03_left_feature_panel.jpg` shows the feature tree where these dialogs land. The dialogs themselves should match Onshape's parametric feature dialogs in density and clarity. Every new feature/tool task in TASK_QUEUE.md must comply with this standard.
