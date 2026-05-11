# 20. Fix Avalonia/WinForms namespace clash

## Goal
Eliminate the System.Windows.Forms transitive reference and resolve the CS0104 ambiguous-type errors so `dotnet build` returns exit_code 0. Until this lands, no other task can be properly verified — it is the unblocker for the whole loop.

## Scope
- Identify why `My3DApp.csproj` (or one of its dependencies) is pulling in System.Windows.Forms.
- Remove the WinForms reference if it's direct, or shield the dialog code from the ambiguity if removal is non-trivial.
- Fix the three concrete CS0104 errors in:
  - AvaloniaApp/Dialogs/ToolDialogWindow.axaml.cs (lines 20 and 29 referencing `Control`)
  - AvaloniaApp/Dialogs/LineSketchToolOptionsView.axaml.cs (line 11 referencing `UserControl`)
- Acceptable approaches (pick the simplest that works):
  a. Remove the WinForms ProjectReference / FrameworkReference / `<UseWindowsForms>true</UseWindowsForms>` from My3DApp.csproj if present directly.
  b. If WinForms is pulled in transitively from a dependency we need (WebView2 sometimes does this), qualify the ambiguous types in the dialog files — `Avalonia.Controls.Control`, `Avalonia.Controls.UserControl` — or add `using AC = Avalonia.Controls;` aliases.
- After the change, write `AgentOps/build_request.txt` and read `AgentOps/build_result.json` to confirm exit_code 0.

## Out of scope
- Any other build warnings unrelated to the namespace clash.
- Refactoring the dialog scaffold beyond the minimum needed for the build to pass.

## Files likely involved
- `My3DApp.csproj` — primary suspect for the WinForms reference.
- `AvaloniaApp/Dialogs/ToolDialogWindow.axaml.cs` — fix CS0104 for `Control`.
- `AvaloniaApp/Dialogs/ToolDialogWindow.axaml` — possibly nothing, but check for x:Class type refs.
- `AvaloniaApp/Dialogs/LineSketchToolOptionsView.axaml.cs` — fix CS0104 for `UserControl`.
- `AvaloniaApp/Dialogs/LineSketchToolOptionsView.axaml` — same caveat.

## Expected behavior (acceptance)
1. `dotnet build` exits with code 0 (verified via build runner).
2. The three CS0104 errors disappear from the build log.
3. The dialog scaffold still works at runtime — clicking Line still opens the modal, OK/Cancel/Esc/Enter behavior preserved.
4. No new WinForms references introduced anywhere.
5. If WinForms was kept as a transitive dependency, the dialog code uses qualified Avalonia types and there's a one-line comment in each affected file explaining why.

## Notes / hints
- Check `My3DApp.csproj` first — `<UseWindowsForms>true</UseWindowsForms>` or a direct `<PackageReference>` would explain it. That's the cleanest fix to remove.
- Blueprint forbids WinForms references; this is alignment work, not a refactor.

## Verifier checklist
- Build runner returns exit_code 0, errors=0.
- `grep -r "System.Windows.Forms" AvaloniaApp/ Engine/ Backends/ Core/ Studio/ Export/` returns empty (or only the qualified-type comment lines).
- User-side smoke: click Line in sketch mode, dialog appears, Cancel/OK work.
- No warnings count increased compared to baseline.
- One commit in git history with the fix.
