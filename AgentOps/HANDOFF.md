Done (2026-05-10):
- WebViewportHost.cs: Increased THREE.js PointsMaterial size from (preview:6, committed:4)px to (preview:18, committed:16)px — sketch points now visible at CAD-appropriate scale matching the origin marker
- SoftwareViewportControl.cs: Increased software-fallback markerSize from (preview:3.2f, committed:3.8f) to (preview:6.0f, committed:7.0f)
- CadProjectStore.cs: BuildLinePreview — when PendingLineStart is null, now returns [CadSketchPoint at cursor] instead of [] — gives cursor feedback before first click
- CadProjectStore.cs: BuildRectanglePreview — when PendingShapeAnchor is null, now returns [CadSketchPoint at cursor]; when cursor == anchor (distance ≤0.001), returns [CadSketchPoint at anchor]
- CadProjectStore.cs: BuildPreviewEntities switch — added CadSketchToolKind.Point case that returns [CadSketchPoint at cursor], giving Point tool a preview cursor

Not done:
- dotnet build not runnable on this Linux host (targets net10.0-windows); syntax reviewed manually — changes are minimal and mechanically correct

Broken:
- none expected

Next:
- Build verification on Windows; spot-check: enter sketch mode, hover mouse → cursor point visible at 16-18px; place line start → anchor + rubber-band line visible; rectangle after first corner → 4-line preview visible
