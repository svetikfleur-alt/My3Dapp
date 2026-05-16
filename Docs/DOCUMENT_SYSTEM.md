# Document System

My3DApp uses a JSON-based document format with built-in autosave and recovery.

---

## File format

Documents are saved as `.my3dapp` files — JSON with the following top-level envelope:

```json
{
  "schemaVersion": 2,
  "appVersion": "1.0.0",
  "documentId": "a8f3c1e2-...",
  "documentName": "Gear Bracket",
  "createdAt": "2026-05-16T10:00:00Z",
  "modifiedAt": "2026-05-16T11:23:45Z",
  "activeStudio": "Part Studio 1",
  "studios": [
    {
      "name": "Part Studio 1",
      "project": { ... }
    }
  ]
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `schemaVersion` | int | Format version; current value is **2** |
| `appVersion` | string | App version that saved the file |
| `documentId` | UUID | Stable document identity (persists across saves) |
| `documentName` | string | Editable display name |
| `createdAt` | ISO 8601 | UTC timestamp of first save |
| `modifiedAt` | ISO 8601 | UTC timestamp of last save |
| `activeStudio` | string | Name of the active Part Studio at save time |
| `studios` | array | One entry per Part Studio tab |

Each `studios` entry contains:
- `name` — Part Studio tab name
- `project` — the full `CadProject` object (bodies, features, sketch sessions, reference planes, selection state)

### Schema versioning

The reader rejects files saved with `schemaVersion` **greater** than the current build's version. Files saved with older schema versions are always accepted.

---

## File locations

| Location | Purpose |
|---|---|
| User-chosen path | Manual save / open via toolbar or Ctrl+S / Ctrl+O |
| `%APPDATA%\My3DApp\Autosave\autosave.my3dapp` | Rolling autosave (every 2 minutes while dirty) |
| `%APPDATA%\My3DApp\settings.json` | App settings including recent-files list |

---

## Autosave

`AutosaveService` runs a background timer that fires every **2 minutes**. It saves only when `HasUnsavedChanges` is `true`. The autosave path is always the same fixed file — it is a rolling backup, not a versioned history.

On each clean close (user-initiated or keyboard shortcut with no unsaved changes), the autosave file is deleted.

---

## Recovery

On startup, `MainWindow.OnWindowOpened` checks whether `AutosaveService.HasAutosave` is `true`. If it is, a confirmation dialog offers to restore the autosave:

- **Restore** → opens the autosave file; the user can then Save As to a permanent location
- **Discard** → deletes the autosave file and starts with an empty document

---

## Recent files

The recent-files list is stored in `%APPDATA%\My3DApp\settings.json` under the `recentFiles` key. Up to **10** entries are kept. The list is updated on every successful `SaveProject` or `OpenProject` call.

The toolbar "Recent" button (▾) opens a flyout menu listing recent files by filename. Hovering shows the full path as a tooltip.

---

## New document

Ctrl+N (or the **+** toolbar button) creates a new empty document. If there are unsaved changes, a confirmation dialog prevents accidental data loss.

---

## Implementation

| Class | File |
|---|---|
| `StudioWorkspaceController` | `AvaloniaApp/Services/StudioWorkspaceController.cs` |
| `AutosaveService` | `AvaloniaApp/Services/AutosaveService.cs` |
| `AppSettings` | `AvaloniaApp/Services/AppSettings.cs` |
| `StudioShellViewModel.SaveProject` | `AvaloniaApp/ViewModels/StudioShellViewModel.cs` |
| `StudioShellViewModel.OpenProject` | `AvaloniaApp/ViewModels/StudioShellViewModel.cs` |
| `StudioShellViewModel.NewProject` | `AvaloniaApp/ViewModels/StudioShellViewModel.cs` |
| `MainWindow.OnClosing` | `AvaloniaApp/MainWindow.axaml.cs` |
| `MainWindow.OnWindowOpened` | `AvaloniaApp/MainWindow.axaml.cs` |
