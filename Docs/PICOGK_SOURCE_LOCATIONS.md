# PicoGK / ShapeKernel / Zoo Local Source Locations

Generated on 2026-05-24 during the `feature/picogk-shapekernel-bringup` rescue pass.

## Found Local Sources

| Path | Source | Type / language | Build system | Git tracking | Notes |
|---|---|---|---|---|---|
| `ThirdParty/KittyCAD.Zoo/upstream/modeling-app-1.2.6` | Zoo v1.2.6 | TypeScript, Rust, web assets | npm / Rust workspace style upstream tree | Not listed by `git ls-files`; treated as local reference-only source | Useful for architecture, document/project ideas, KCL samples, UI/workspace patterns. |
| `ThirdParty/LEAP71.ShapeKernel/upstream/LEAP71_ShapeKernel-main` | ShapeKernel local snapshot | C# examples + empty library folder skeleton | No `.csproj` found locally | Not listed by `git ls-files`; treated as local reference-only source | Snapshot is incomplete: examples exist, but `ShapeKernel/` library source folders are empty. |
| `PicoGK-main.zip` | PicoGK local source archive | C# + native runtime | .NET project inside zip | Not listed by `git ls-files`; treated as local reference archive | Practical bring-up source. Extracted temporarily for inspection only. |
| `References/zoo/*` | Zoo screenshots / behavior references | PNG screenshots | n/a | Tracked | Good for UX reference only, not source integration. |
| `References/onshape/*` | Onshape screenshots / behavior references | PNG screenshots | n/a | Tracked | UX reference only. |

## Tracking Notes

- `git ls-files` only returned tracked files under `References/zoo`.
- The real source trees under `ThirdParty/` were **not** listed by `git ls-files` during this pass.
- Because those sources are local drop-ins and not part of the active app build, they should remain **reference-only** until there is a deliberate vendoring decision.

## Source Quality Summary

- **Zoo v1.2.6**: good local upstream reference tree.
- **ShapeKernel**: incomplete local snapshot; examples refer to missing `ShapeKernel` library code.
- **PicoGK**: recoverable via local zip + already cached NuGet package in the app environment.
