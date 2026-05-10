# 46. Assistant context injection — wire action log and CAD state into system prompt

## Goal
Blueprint 08 ("Event and Context Model") states: _"The assistant cannot reason well without context. The system must expose structured context."_ and _"Assistant reasoning must be context-fed, not hallucination-led."_ Currently `AssistantChatService.BuildSystemPrompt()` is a static string with no session state — it does not include the current mode, selected objects, active tool, sketch state, or the `_actionLog` that already exists and is populated in `StudioWorkspaceController`. This task wires the context through so the assistant can give grounded, session-aware answers.

## Scope
**C# side — context assembly:**
- Add a `CadAssistantContext` record / class (in `Engine/` or `AvaloniaApp/Services/`) containing:
  - `string Mode` — current assistant mode (Auto/Do/Think/Assist/ThinkAndDo).
  - `string AppMode` — "Sketch" or "3D".
  - `string? ActiveTool` — current sketch tool name if in Sketch mode, else null.
  - `string? SelectedPlane` — active sketch plane (e.g. "Top") if in a sketch session.
  - `IReadOnlyList<string> SelectedFeatures` — names/types of currently selected tree nodes.
  - `IReadOnlyList<string> RecentActions` — last 10 lines from `_actionLog` in `StudioWorkspaceController`.
  - `int SketchEntityCount` — number of entities in the active sketch, or 0.
  - `int BodyCount` — number of solid bodies in the project.
- `StudioWorkspaceController` exposes a method `GetAssistantContext()` → `CadAssistantContext` that assembles the above from current state.

**AssistantChatService:**
- Extend `BuildSystemPrompt(string mode)` → `BuildSystemPrompt(CadAssistantContext ctx)`.
- The new system prompt includes: the static engineering-assistant description, plus a structured `## Context` block serialized from `CadAssistantContext` (plain text, not JSON, to keep token cost low).
- Example context block:
  ```
  ## Session context
  App mode: Sketch | Plane: Top | Active tool: Line | Sketch entities: 4 | Bodies: 2
  Recent actions:
    [14:01:02] Entered sketch on Top plane
    [14:01:08] Placed Line (0,0)→(20,0)
    [14:01:14] Applied Horizontal constraint
  Selected: (none)
  ```
- `GetAssistantContext()` is called at the moment a message is sent (not stored); context is always fresh.
- The `command` JSON response path is unchanged.

**Wiring:**
- `StudioShellViewModel` (or wherever `AssistantChatService.SendAsync` is called) passes the context object into the call.
- No new UI required — context is invisible to the user (it's in the system prompt only).

## Out of scope
- Showing the context block to the user in the assistant panel (future transparency feature).
- Streaming the context into every interim token (only on send).
- Storing context history across sessions.
- Any changes to the assistant UI layout (covered by task 25, already done).

## Files likely involved
- `Engine/CadAssistantContext.cs` — new record (or add to `CadModel.cs`).
- `AvaloniaApp/Services/StudioWorkspaceController.cs` — add `GetAssistantContext()`.
- `AvaloniaApp/Services/AssistantChatService.cs` — replace `BuildSystemPrompt(string)` with `BuildSystemPrompt(CadAssistantContext)`.
- `AvaloniaApp/ViewModels/StudioShellViewModel.cs` — pass context when calling `AssistantChatService.SendAsync`.

## Expected behavior (acceptance)
1. Send a message to the assistant while in Sketch mode with 3 entities on the Top plane; the LLM response references the correct mode or tool without being prompted explicitly.
2. Send "what did I just do?" while 2 action-log entries are recent; LLM response reflects the last actions (it sees them in system prompt).
3. `BuildSystemPrompt` no longer has a static signature taking only `string mode`.
4. Build: 0 errors, 0 warnings.
5. No regression in existing assistant send flow (messages still sent, API key still required, command parse still works).

## Notes / hints
- Keep the context block short (< 300 tokens) — it's injected on every request. Trim `_actionLog` to last 10 entries.
- Do NOT pass the entire feature tree or full sketch entity list — just counts. Verbose context can be added in a later refinement task.
- `_actionLog` is `private` in `StudioWorkspaceController` — expose via `GetAssistantContext()` method rather than making it public directly.
- `GetAssistantContext()` should be safe to call from any thread (snapshot the list with `.ToList()` or `.TakeLast(10).ToArray()`).
- Blueprint 08 section "Context sources" enumerates exactly what to include — follow it.

## Verifier checklist
- [ ] Build: 0 errors, 0 warnings.
- [ ] `BuildSystemPrompt` accepts `CadAssistantContext` (static string overload removed or updated).
- [ ] System prompt logged/traceable contains mode, app mode, recent actions.
- [ ] Sending a message while in Sketch mode with entities — response is contextually appropriate.
- [ ] Sending a message in 3D mode with selected body — response is contextually appropriate.
- [ ] No regression in API key check, mode chip, or command parse path.

## Complexity
Low-medium. Pure plumbing — no new UI, no new engine types. Main risk is threading (snapshot the log safely) and keeping the context block concise.
