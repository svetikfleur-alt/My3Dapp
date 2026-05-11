# 25. Assistant panel — fix and bring to baseline

## Goal
The assistant panel in the studio is currently in a weird state: user reports it shows a message saying "key is not configured" even though the key appears to be configured, and overall the panel doesn't feel right. Bring it to a normal baseline that matches `Blueprint/Assistant/` and the Zoo Zookeeper reference visually, while staying true to the Blueprint principle that the assistant is a structured engineering helper, NOT a chatbot.

## Scope
- Audit the current state of the assistant panel: what UI elements exist, what's wired, what's not, what's showing a wrong message and why.
- Fix the "key not configured" misreport. Trace the key check: is it reading from the correct location (env var, settings file, UI input)? Is the comparison against an empty string vs null vs absent? If the user has set the key, the panel must reflect that.
- Make the panel match Blueprint/Assistant docs (06, 08): mode selector with the five modes (Auto / Do / Think / Assist / Think&Do), structured message area, action-chip area for suggested next steps, input bar at the bottom, NOT message bubbles with avatars.
- Visual fit: take cues from `References/zoo/blocks/06_right_zookeeper_panel.jpg` for layout (input position, panel width, structure) but explicitly drop the chat-app aesthetics (no big avatars, no friendly emoji, no "How can I help today?" copy — keep it terse and engineering-y).
- Make sure the panel state is reactive: when the key gets set/cleared, the UI updates immediately without needing a restart.

## Out of scope
- Real LLM wiring / actual reasoning calls (separate task; here we only fix the shell).
- New modes beyond the five from Blueprint.
- Chat history persistence.
- Multi-conversation tabs.

## Files likely involved
- `AvaloniaApp/Controls/AssistantPanel*` or wherever the right-side panel is hosted.
- Settings / configuration files where the key is stored.
- Whatever ViewModel exposes the "key configured" state.
- Resource / theme files that style the panel.
- `Blueprint/Assistant/06-*.md` and `Blueprint/Assistant/08-*.md` — read these first to understand the intended behavior.

## Expected behavior (acceptance)
1. With the API key set: panel shows "ready" state (or whatever Blueprint defines), NO "key not configured" warning.
2. With the API key unset: panel shows a clear, terse "key needed" message with a one-click way to set it (settings dialog or inline input). Not a full-screen blocker — the rest of the studio still works.
3. The five modes (Auto / Do / Think / Assist / Think&Do) are selectable as a segmented control or pill row at the top of the panel.
4. Message area scrolls; new entries append at the bottom; long entries wrap cleanly.
5. Action-chip area sits above the input bar and can render 0–N chips.
6. Input bar at the bottom: text input + send button. Enter sends, Shift+Enter newline, Esc clears.
7. Visual style: dense, technical, no SaaS chat aesthetics. Theme parity (Light/Dark) — every element looks correct in both.
8. Panel state updates immediately when key changes (reactive binding, no restart).
9. Build returns 0 errors, 0 warnings.

## Notes / hints
- Reference visual: `References/zoo/blocks/06_right_zookeeper_panel.jpg` for layout, but ignore content (Zoo screenshot was in a paywall state — that's not the model state we want).
- Reference: `References/user_v0_prototype/v0_main.jpg` — user's own v0 mock has the right structure (modes, quick prompts, "Apply suggestion" button).
- Blueprint: `Blueprint/Assistant/06`, `Blueprint/Assistant/08` — authoritative.
- Key storage: check existing settings / config plumbing first; do NOT introduce a new storage scheme. If the key is stored as env var, read from `Environment.GetEnvironmentVariable`; if in a settings file, use the existing settings service.
- Don't add chat-bubble avatars / emoji / "Hello!" copy.

## Complexity
Moderate. Multi-file (panel UI + view-model + settings plumbing) and reactive binding nuances, but no deep geometry or kernel changes. Sonnet should handle. Bump to Opus only if the settings/binding plumbing turns out to be very tangled.

## Verifier checklist
- Build returns 0 errors, 0 warnings.
- Run app: assistant panel visible on the right.
- With key set in current settings: NO "key not configured" warning.
- Clear the key (via settings or env var): panel shows the "key needed" prompt within a couple seconds (reactive).
- Switch all five modes — selector responds, each mode is visibly active.
- Type into input, press Enter — message lands in the area (even if assistant stub responds with placeholder).
- Theme switch Light/Dark — panel stays styled correctly in both.
- One git commit with the change.
