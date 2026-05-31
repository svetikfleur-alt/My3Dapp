# UI Command Truth Inventory

## Textual Menus (Top Left)

### File
- **New Project...**: WORKING (Opens dialog, invokes logic)
- **New Large Project**: WORKING (Stubs template, starts project)
- **New Standard Project**: WORKING 
- **New Quick Design**: WORKING
- **Open Existing Project...**: WORKING (Opens picker)
- **Recent Projects**: WORKING (Context menu shows recents)
- **Save**: WORKING
- **Save As...**: WORKING

### Create
- **Box, Cylinder, Sphere**: WORKING (Creates primitives)

### Sketch
- **Start Sketch, Finish Sketch, Cancel Sketch**: WORKING
- **Line, Rectangle, Circle, Arc, Point**: WORKING (Enters specific sketch modes)

### Solid
- **Extrude**: WORKING
- **Revolve**: EXPERIMENTAL MESH MVP
- **Sweep**: EXPERIMENTAL MESH MVP
- **Loft**: EXPERIMENTAL MESH MVP

### Modify
- **Fillet**: LIMITED MVP (2D Extrude Base corners only)
- **Chamfer**: LIMITED MVP (2D Extrude Base corners only)
- **Hole**: LIMITED MVP (Centered Boolean Cut)
- **Shell**: LIMITED MVP (Extrude bases only)
- **Linear Pattern**: LIMITED MVP (Body-level duplicate)
- **Circular Pattern**: LIMITED MVP
- **Mirror Body**: LIMITED MVP
- **Booleans (Union, Subtract, Intersect)**: WORKING (Mesh Booleans)

### Transform
- **Move Tool**: WORKING
- **Create Datum Plane**: WORKING (Opens offset datum dialog)

### View
- **Focus / Fit View**: WORKING
- **Command Palette**: WORKING (Opens search palette)

### AI
- **Open Copilot**: WORKING (Opens right panel)
- **Configure AI...**: STUB-ish (Opens dialog but might need validation)

### Recipe
- **Open ACL Panel**: WORKING
- **Open Sample Recipe**: WORKING
- **Run Active Recipe**: WORKING

### Export
- **Prepare Workspace**: WORKING (Toggles mode)
- **Export STL / OBJ...**: WORKING (Opens dialog)

### Experimental
- **(Removed)**: The "Experimental" menu has been removed. All solid and modify features have been moved into "Solid" and "Modify" menus and explicitly labeled with their MVP limitations in the UI.

## View Modes
- **Mesh Debug**: WORKING. Toggles `material.wireframe` in WebGL which shows a raw triangle mesh. 
  - *Previously named "Wire". Renamed to clarify it is not a true CAD wireframe.*

## Right Panel Tabs
- **Properties, Templates, Library**: Real panels. Empty states exist.
- **Copilot**: Real.
- **Recipe / AI Script**: Real code execution.

## Dialogs
- **Export, Extrude, Fillet, Hole, Shell, Chamfer, etc.**: WORKING. Properly validate inputs and handle backend failures gracefully.

## Action Plan
- [x] 1. Re-organize "Experimental" menu into "Solid" and "Modify" groups in `MainWindow.axaml`.
- [x] 2. Fix the "Wireframe" viewport toggle text to "Mesh Debug".
- [x] 3. Check dialogs for silent failures and add explicit "Not Implemented" reasons if any remain (all are now wired to PicoGK).
- [x] 4. Improve contrast/borders in `Studio.Dark.axaml` and `Studio.Light.axaml` to make the UI look more solid.
