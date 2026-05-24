# PicoGK / ShapeKernel Integration TODO

## Immediate Next Fixes

1. Add one experimental recipe execution path that targets `IGeometryKernelAdapter`.
2. Add adapter-backed smoke tests for:
   - create box
   - create cylinder
   - subtract
   - export STL
3. Decide where to store backend choice in document or feature metadata.

## Missing Dependencies / Inputs

- Complete local ShapeKernel source drop is missing.
- Upstream PicoGK local source build depends on packages/framework refs that are not available offline in this environment.

## Build Blockers

- ShapeKernel local snapshot has examples but missing actual library implementation files.
- PicoGK upstream project restore fails offline.

## Adapter Tasks

- add sphere / cone / shell operations to PicoGK adapter if needed
- expose capability flags to future command executor selection
- add consistent diagnostics for adapter failures
- decide whether future app integration should host a dedicated long-lived PicoGK session or use one-shot worker sessions

## Operations To Support Next

- shell/open-top enclosure operations
- offset/rounding via PicoGK voxel offset functions
- mesh-first export of experimental bodies into the existing Prepare flow

## Risks

- PicoGK is voxel-based; dimensional fidelity depends on chosen voxel size
- only one PicoGK `Library` configuration can run at a time
- ShapeKernel examples may encourage assumptions that cannot be validated until the full source is available

## Recommended Next Branch

`feature/picogk-adapter-recipe-prototype`
