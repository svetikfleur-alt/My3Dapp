# 15. Error / message surface

## Goal
Add a non-blocking notification strip so build / save / load / extrude / sketch errors and informational messages are surfaced visibly, instead of disappearing into the log tab.

## Scope
- Notification strip at top OR bottom of the viewport (above status bar).
- Severity levels: info, warning, error.
- Auto-dismiss for info (5s); warning persists until acknowledged; error persists until dismissed.
- Click strip to expand details (full message + optional log link).
- Dedupe identical consecutive messages.
- Wire current silent-failure paths to emit notifications (Save fail, Load fail, Extrude validation fail, Sketch finish with open profile, etc.).

## Out of scope
- Toast stack (multiple simultaneous notifications) — single strip slot is fine.
- Notification history view.
- Localization.

## Files likely involved
- New: `AvaloniaApp/Controls/NotificationStrip.axaml(.cs)`.
- `AvaloniaApp/MainWindow.axaml(.cs)` — host the strip.
- `AvaloniaApp/Services/RuntimeLog.cs` (existing) — push notifications when log severity >= warning, OR a sibling `NotificationService.cs`.
- `Engine/CadProjectStore.cs` — emit notifications from validation failures.

## Expected behavior (acceptance)
1. Triggering a Save with a read-only path -> red error strip "Save failed: …".
2. Extruding an open profile -> warning strip; Extrude dialog still shows inline error.
3. Successful Save -> green info strip "Saved" auto-dismisses in 5s.
4. Click strip -> expands to show details; click X to dismiss.
5. Identical consecutive warnings don't stack.

## Notes / hints
- Strip should not occlude geometry; constrain height (~28 px).
- Wire through one notification service so future code emits cleanly.

## Verifier checklist
- [ ] Build runner ok.
- [ ] Trigger save fail (read-only path) -> error strip appears.
- [ ] Trigger open-profile extrude -> warning strip.
- [ ] Successful Save -> info strip auto-dismisses.
- [ ] Dedup behavior verified by triggering same warning twice.
