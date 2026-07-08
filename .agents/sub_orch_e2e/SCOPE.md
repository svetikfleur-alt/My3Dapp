# Scope: E2E Testing & Audit

## Architecture
The E2E test suite validates the integration of:
1. **ACL Parser and Compiler**: Compiles source code to `ExactFeatureGraph`.
2. **Exact Feature Graph / Nodes**: Represents variables, sketches, extrudes, holes, and patterns.
3. **OCCT Exact Kernel (`OcctExactKernel`)**: Performs 3D B-Rep operations.
4. **Exact Model Session (`ExactModelSession`)**: Coordinates parsing, validation, execution, rollback, and state.
5. **AST-aware Edit Service (`IAclSourceEditService`)**: Performs transactional patches.

The E2E tests act as an opaque-box harness calling these APIs, verifying final 3D body outputs (bounds, volumes, face counts), transactional states (STALE flags, rollbacks), and STEP exports.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Test Plan & Infrastructure | Define E2E test suite structure, setup directories, design tests for Tiers 1-4 | None | IN_PROGRESS |
| 2 | Tier 1 & 2 Test Suite Implementation | Code and run test cases for Features 1-5 (Feature Coverage & Boundary cases) | M1 | PLANNED |
| 3 | Tier 3 & 4 Test Suite Implementation | Code and run test cases for Cross-Feature interactions & Real-World scenarios | M2 | PLANNED |
| 4 | State, Transactional, & Rollback Tests | Validate rollback, STALE flags, invalid patches, preview disposal | M3 | PLANNED |
| 5 | Publication & Verification | Run Forensic Audit, generate STEP files, write TEST_INFRA.md, TEST_READY.md | M4 | PLANNED |

## Interface Contracts
### E2E Test Runner ↔ Engine Modules
- Test suite invokes `AclExactCompiler.Compile()` to build feature graphs.
- Test suite invokes `ExactModelSession.TryUpdateModel()` to build shapes and test transactional updates.
- Test suite inspects `OcctBodyHandle.GetBounds()` and queries volume to assert exact shape dimensions.
- Test suite invokes `IExactCadExporter.ExportStep()` to output STEP files, which are validated for BREP structure.
