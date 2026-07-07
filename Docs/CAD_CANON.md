# CAD CANON (authoritative product rules)

My3DApp is a local-first, Onshape-like parametric CAD application.

## The pipeline (one engine, no parallel sources of truth)

```
ACL source (or UI operation)
  -> ACL AST + source map
  -> parametric feature graph
  -> exact Solid / B-Rep Body (OCCT)
  -> native CAD-aware viewport (OCCT AIS/V3d in Avalonia)
  -> export (STEP exact; STL/3MF via transient tessellation; Parasolid via adapter, pending SDK)
```

## Rules

1. **ACL is the editable model.** Save = save ACL source. Open = open ACL source.
   No proprietary project container (.umxproj is legacy, read-only import shim only).
2. **Exact Solid is the model.** Mesh may enter, mesh may leave, mesh may not live
   inside the authoritative model (no MeshBody, no persistent DisplayMesh, no mesh
   in ACL/AST/feature graph/history/assistant context).
3. **The native viewport displays it.** WebView2/Three.js is legacy, being removed
   from the primary CAD path. No new product-critical features on it.
4. UI edits and ACL edits converge on the same AST (source-aware edits both ways).
5. Feature-tree entries map to ACL nodes and source locations.
6. No fake capabilities: every command has a status — WORKING / LIMITED /
   IN_DEVELOPMENT / HIDDEN. No no-op buttons, no fake success.
7. Assistant (DeepSeek default, Claude explicit recovery) proposes ACL patches that
   are validated locally and applied only after user approval and successful
   regeneration.

## Core MVP gate

Start Page -> new/open ACL -> Part Studio -> plane -> sketch (rect/circle,
constraints, driving dimensions, Solver Lite) -> extrude -> hole (boolean subtract)
-> linear pattern -> edit dimension -> regenerate -> ACL sync -> save/reopen ->
STEP export verified externally -> STL/3MF export.
