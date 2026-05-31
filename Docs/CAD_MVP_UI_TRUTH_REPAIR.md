# CAD MVP UI Truth Repair

The current My3Dapp UI has components that act as placeholders or are mislabeled. This plan executes the UI truth repair to ensure that only working commands are active, stubs are disabled with clear reasons, and the UI contrast feels like a solid CAD application.

## Proposed Changes

### 1. Fix Text Menus and Command Wiring
- **[MainWindow.axaml]**
  - Remove the "Experimental" top-level menu.
  - Move fully working features (Revolve, Sweep, Loft, Quick Export STL) into appropriate native menus (e.g., Solid, Export).
  - Move edge/body modifiers (Fillet, Chamfer, Hole, Shell) into a new **Modify** text menu.
  - Move patterning (Linear/Circular Pattern, Mirror) and Booleans (Union/Subtract/Intersect) into the **Modify** menu.
  - Ensure any stub tools are properly disabled.

### 2. View Mode Honesty
- **[WebViewportHost.cs]**
  - Rename the viewport rendering toggle from "Wire" to "Mesh Debug", as it currently renders raw WebGL triangles.

### 3. UI Readability and Contrast Polish
- **[Studio.Dark.axaml] / [Studio.Light.axaml]**
  - Increase border contrast and control visibility to remove the "pale mockup" look.
  - Improve selected state styles for toggles.

### 4. Dialog Truth
- Verify that dialogs (like `ExportDialog.axaml`, `AiSettingsDialog`) correctly validate fields and don't pretend to work if underlying APIs are disconnected. Disable confirm buttons with a clear message if fundamentally incomplete.

## Verification
- Build and run the app to ensure visual consistency and honest reporting of feature availability.
