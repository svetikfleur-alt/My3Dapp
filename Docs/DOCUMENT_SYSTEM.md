# Document System

UMX1 Studio stores projects as JSON-based document files.

## Extension

- preferred: `.umxproj`
- legacy compatibility: `.my3dapp`

## Persisted data

Documents currently persist:
- schema version
- app version
- document id
- document name
- active part studio
- part studio snapshots
- CAD project data
- active workspace kind
- selected template id
- template parameter values
- export history summary

## Autosave

Autosave is stored under:

`%APPDATA%/My3DApp/Autosave/`

The app checks for autosave on startup and offers restore/discard recovery.

## Recent files

Recent files are stored in:

`%APPDATA%/My3DApp/settings.json`

## Current limitation

The document system is already useful, but not yet a full production collaboration/versioning system.
