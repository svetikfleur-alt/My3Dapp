# Studio Layout v1

## 1. Diagnosis

Product identity, architecture, and assistant behavior are already fixed by Blueprint (desktop-first parametric CAD Studio; five-layer architecture — UI Shell / Viewport / Model / Assistant / Event-Context; assistant is a non-chatty engineering co-pilot with modes Auto / Do / Think / Assist / Think & Do; forbidden patterns: SaaS dashboards, chat bubbles, fake widgets, browser visual language). What is **not** yet fixed is the concrete studio window layout — zone geometry, panel behavior, how Sketch vs 3D state shows in the UI, and how the mode switcher, message area, and input bar of the assistant panel are physically arranged. This document synthesizes that layout from three references: the user's v0 prototype (baseline direction), Zoo Design Studio (code + chat panel layout), and Onshape (feature tree + Part Studio structure). It does not decide shell technology (WPF vs Avalonia — see Architecture/04) and it does not define visual theme.

## 2. Overall layout

Six zones, fixed shell, resizable splitters.

```
┌──────────────────────────────────────────────────────────────────┐
│ Top bar (commands, doc title, mode indicator)                   ~36px
├──────────┬─────────────────────────────────────────┬─────────────┤
│          │                                         │             │
│  Left    │          Viewport                       │   Assistant │
│  panel   │          (dominant zone)                │   panel     │
│          │                                         │             │
│  ~260px  │                                         │   ~340px    │
│          │                                         │             │
│          ├─────────────────────────────────────────┤             │
│          │  Code editor (optional, docked bottom)  │             │
│          │  ~30% height when open                  │             │
├──────────┴─────────────────────────────────────────┴─────────────┤
│ Status bar (selection, units, active tool, assistant state)     ~22px
└──────────────────────────────────────────────────────────────────┘
```

**Proportions (initial targets, 1920×1080 baseline):**
- Left panel: 260 px, min 200, max 420, collapsible to 36 px rail.
- Assistant panel: 340 px, min 280, max 520, collapsible to 36 px rail.
- Viewport: fills remaining width. Always present, never collapsible.
- Code editor (optional): docks as a horizontal pane below the viewport, not alongside it. Default closed. When open, 28–32 % of viewport column height. Toggle via shortcut (`Ctrl+E` candidate) and a status-bar indicator.
- Top bar: 36 px, fixed. Status bar: 22 px, fixed.

**Resize/collapse behavior:**
- All splitters are draggable with snap at default widths.
- Panel collapse is to a **vertical icon rail**, not hidden — the user must always see the rail exists. No "hidden mystery state."
- Code editor is the only pane that fully hides (because it is optional).

**Sketch vs 3D mode separation:**
- Mode is a **global studio state**, not a per-panel toggle. Entering Sketch swaps the left-panel lower section (tool groups), changes the viewport chrome (axes swap to 2D, grid appears, view normalizes to sketch plane), and swaps the top bar's secondary row. The Feature Tree remains visible and scoped.
- The mode indicator in the top bar shows current mode with an explicit badge (`SKETCH` / `3D`). No ambiguous dual state.
- Assistant panel does **not** change mode visually — it reads mode from the Event/Context layer and adapts its suggestions only.

## 3. Zone-by-zone spec

### 3.1 Top bar — Commands

**Purpose:** global actions and navigation. Not a ribbon.

**Contents (left → right):**
- App/document title (doc name, part studio name, modified-dot).
- `Commands` button (opens command palette — `Ctrl+K`). Borrowed from Zoo.
- Mode indicator badge: `SKETCH` / `3D`.
- Save state / sync indicator (local only for now).
- Right-aligned: overflow menu (settings, help). No Share/Publish in MVP — those are SaaS patterns.

**Behavior:** single row, fixed height, no tabs embedded here. Keyboard shortcut focus visible.

**Inspiration:**
- Zoo top bar: Commands palette + minimal chrome → take.
- Onshape breadcrumb doc/tab header → take the doc-title position, skip the multi-tab strip for now (no multi-document MVP).

### 3.2 Left panel — Feature Tree + Mode + Tools

**Purpose:** structural view of the model + modal tool access.

**Contents (top → bottom):**
1. **Mode switch** — two explicit buttons `Sketch` / `3D`. Active state clearly shown. Not a dropdown. (Entering Sketch requires a plane selection — standard CAD gate.)
2. **Filter / search** — filter-by-name field over the tree (Onshape pattern).
3. **Default geometry** — Origin, Top, Front, Right. Always present.
4. **Features** — parametric history list with a count (`Features (4)`). Collapsible. Active feature bold. Rollback hook reserved (future).
5. **Parts** — count list (`Parts (0)`). Collapsible.
6. **Tool strip** — docked along the left edge of this panel (icon column), switches between Sketch tools and 3D feature tools based on current mode. Icons + tooltip, no text labels.

**Behavior:**
- Tree items selectable; selection flows to Event/Context layer, which assistant reads.
- Right-click context menu on tree items (hide/show, rename, edit, delete, suppress). Onshape pattern.
- Collapsed state shows a 36 px icon rail with mode switch + filter icon + a single "tree" icon that reopens the panel.

**Inspiration:**
- v0 prototype: `Sketch` entry + Features tree with Origin/Top/Front/Right → direct take.
- Onshape: filter-by-name, Features (n) / Parts (n) headers, context menus → take.
- Zoo: left-rail icon strip when collapsed → take collapse behavior.

### 3.3 Viewport

**Purpose:** the dominant zone. Model lives here.

**Contents:**
- 3D render surface (rendering layer — Blueprint Architecture, layer 2).
- Coordinate gizmo (bottom-left corner of viewport). Axis colors consistent with planes.
- ViewCube / orient widget (top-right). Click-to-align to Top/Front/Right/iso.
- Floating action: `Start Sketch` button — visible only when no sketch active and a plane is selected. Zoo pattern.
- In Sketch mode: a visible 2D grid, sketch-plane highlight, view normalized.
- Viewport chrome is minimal — no panels overlap it.

**Behavior:**
- No assistant output ever renders inside the viewport.
- Camera/navigation via standard CAD bindings (to be specified in Commands doc).
- Selection in the viewport is the same context channel as selection in the tree.

**Inspiration:**
- Onshape viewport cleanliness + coordinate gizmo + clean white background → take.
- Zoo Start Sketch contextual button → take.
- v0 prototype coordinate gizmo placement → take.

### 3.4 Right panel — Assistant

**Purpose:** context-aware engineering helper. Tool panel, not messenger. This is the zone most at risk of drifting into chat UI; spec is deliberately prescriptive.

**Vertical layout (top → bottom, inside the panel):**

1. **Header row (28 px):** `Assistant` label on left; panel collapse button on right. No avatar. No status emoji.
2. **Mode selector (40 px):** segmented control with five buttons: `Auto` · `Do` · `Think` · `Assist` · `Think & Do`. One-line, equal-width, active button filled. This is the primary affordance — it is never hidden, never behind a dropdown. The v0 prototype already established this pattern with four modes; this doc formalizes five per Blueprint/Assistant/06.
3. **Context strip (24 px, read-only):** shows what the assistant is currently reasoning about — e.g. `Top plane · Sketch · 2 entities`. Pulled from Event/Context layer. Dimmed text, monospace-ish. This replaces the "who is speaking" metadata that chat UIs put on every message.
4. **Result area (flex):** structured blocks, **not** bubbles. Each block is one of:
   - **Plan block** — numbered short steps, each with an inline action button if executable.
   - **Recommendation block** — titled, with bullets and an optional `Apply` button.
   - **Progress block** — single line + spinner while executing (Do / Think & Do).
   - **Summary block** — post-execution, what changed, collapsed by default.
   Blocks stack chronologically. No speech bubbles, no avatar gutter. Separators are horizontal rules. Older blocks fade to muted; none disappear.
5. **Action chips row (32 px, conditional):** contextual quick-prompts — e.g. `Create a bracket`, `Explain parametric modeling`, `Ask about modeling`. These are **suggestions**, not the only input path. Row hides if irrelevant. v0 + Zoo both use this pattern; take it, keep it contextual rather than permanent.
6. **Input bar (56 px, fixed at bottom):** single-line text field with a send affordance. A small mode-chip on the left of the input echoes the currently-selected mode so the user sees what will happen when they submit. Placeholder text changes per mode (e.g. `Do: …`, `Think: …`). No smiley pickers, no file drop — text only for now.

**Behavior:**
- Empty state is **intentional**, not fake. The initial state shows: context strip (`No selection`), an empty result area with one muted hint line, and the action chips populated with mode-appropriate starters. **No** "hello I'm your assistant" greeting.
- Apply / Execute buttons are inline in blocks. Clicking triggers the Model layer via the Assistant layer's execution API. If execution is gated (e.g. Sketch mode required), the button is disabled with a tooltip explaining why — not hidden.
- Mode switch at the top is sticky across sessions; last mode is remembered.
- Errors (like Zoo's paywall state) render as a dedicated error block with an explicit dismiss — they never replace the normal assistant surface silently.

**Inspiration:**
- v0 prototype: mode selector, Apply suggestion button, action chips → direct take, expanded from 4 modes to the 5 Blueprint modes.
- Zoo Zookeeper panel: input-bar-at-bottom + chips-above-input geometry, panel width ~340 px, footer disclaimer placement → take **structure only** (see INDEX caveat: that screenshot is paywall state, not representative).
- SolidWorks PropertyManager: structured, block-based, context-sensitive surface → take the philosophy (not the visual weight).

### 3.5 Code editor (optional, Zoo-style)

**Purpose:** expose the parametric definition as editable text when/if we adopt a code-backed representation. Not in MVP critical path.

**Dock location:** below the viewport, horizontal pane. **Not** on the left or right — left is structural, right is the assistant; crowding either dilutes their role.

**Visibility:**
- Default: closed.
- Opened via shortcut / status-bar toggle / command palette.
- When open, takes 28–32 % of the viewport-column height; viewport compresses, does not overlap.

**Contents:**
- Tabs for open files (e.g. `main.kcl`, or whatever representation we adopt).
- Gutter with line numbers.
- Save state dot per tab.
- Syntax highlighting reserved.

**Behavior:**
- Code ↔ model sync is a future architectural question (see Open questions §5). V1 may ship read-only.
- Errors in code surface in a thin strip at the top of the editor, not in the assistant.

**Inspiration:** Zoo — code pane docked below the 3D view with project file tabs. Take the docking direction and the file-tabs pattern. Skip Zoo's video-streamed engine architecture (we are local compute).

### 3.6 Status bar

**Purpose:** one-line system state. Not a dashboard.

**Contents (left → right):**
- Current selection summary (`No selection` / `Sketch on Top plane` / `Edge × 1`).
- Active tool name.
- Units (`mm` / `in`).
- Grid/snap state.
- Right-aligned: assistant state dot (`Idle` / `Thinking` / `Executing`) — mirrored from the assistant panel so the user sees state even when the panel is collapsed.

**Inspiration:** Zoo bottom status bar (`Fast`, `No selection`, disclaimer) → take the compactness and the assistant-state mirror. Skip the disclaimer text on every load.

## 4. Borrow / Reject matrix

### From user's v0 prototype

| Borrow | Reject |
|---|---|
| Three-zone shell (left panel · viewport · right assistant) | v0's SaaS breadcrumb ("Drafts / Publish / Share") — browser pattern, dropped per UI/05 |
| Mode selector as a segmented control, primary affordance | v0's 4-mode set — expanded to 5 per Blueprint/Assistant/06 (add `Think & Do`) |
| Quick-prompt chips above the input bar | v0's trial/upgrade banner area — SaaS, dropped |
| `Apply suggestion` button inline | Any "dashboard" style empty-state card |
| Features tree with Origin/Top/Front/Right defaults | — |

### From Onshape

| Borrow | Reject |
|---|---|
| Part Studio feature-tree structure: Default geometry / Features (n) / Parts (n) | Multi-tab document strip in the top bar — no multi-doc in MVP |
| Filter-by-name field over the tree | Cloud/Share/Explore chrome — cloud-first, violates Core/01 |
| Right-click context menus on tree items and viewport | Browser-shell framing (URL bar, tabs) — we are desktop, Architecture/04 D-002 |
| Clean white viewport with coordinate gizmo | Timeline scrubber at the bottom of the panel — deferred, not MVP |
| Minimalist top bar | — |

### From Zoo Design Studio

| Borrow | Reject |
|---|---|
| `Commands` palette (Ctrl+K) in the top bar | Freeform chat UI — conflicts with Blueprint/Assistant/06 (non-chatty) |
| Input-bar-at-bottom + chips-above-input geometry in the assistant panel | Chat bubbles / avatars / speech-bubble metadata — forbidden per UI/05 |
| `Start Sketch` contextual button in the viewport | `Share` / `Publish` top-bar actions — SaaS, dropped |
| Code editor **docked below viewport**, with file tabs | Video-streamed geometry engine — we are local compute, Core/01 |
| Bottom status bar with assistant state mirror | Permanent disclaimer text on every load — noise, UI/05 |
| Left-rail icon strip on collapse | Billing/account upsell surface in the assistant panel — never surface monetization in the tool |

## 5. Open questions

1. **Code editor representation.** Do we adopt a code-backed parametric definition (Zoo/KCL-style) as the source of truth, keep code as a read-only view of the feature graph, or skip the code panel entirely in v1? Decision gates the Model layer API shape.
2. **Shell technology — WPF or Avalonia.** Architecture/04 D-004 leaves this open. Assistant panel density + custom rendering needs will push the answer. Worth resolving before building the prototype zone.
3. **Mode-switch gating for Sketch entry.** Does clicking `Sketch` from a cold state prompt plane selection, pre-select Top, or show an inline planes list? Affects left-panel copy.
4. **Action-chip source.** Are chips hand-authored per context, generated by the assistant, or a mix? Affects Event/Context data contract.
5. **Collapsed-panel affordance.** Icon rail (always present) vs. edge-tab (hoverable) vs. pure shortcut-only. Icon rail recommended here but not decided.

## 6. Next step

Build the **right assistant panel** as the first real prototype zone.

Rationale:
- It is the highest-risk zone — most likely to drift into chat UI without concrete structure.
- It is the zone where this doc makes the most prescriptive, testable decisions (mode selector, block types, input-bar layout).
- It can be exercised against a mocked Event/Context layer without requiring the Model layer to be real.
- Building it first validates the Blueprint/Assistant concept against an actual surface before the feature tree / viewport are wired.

Scope of the first prototype:
- Panel shell with header, mode selector (5 modes), context strip, result area, chips row, input bar.
- One mocked Plan block, one mocked Recommendation block, one mocked Progress→Summary cycle.
- Mode selector wired to placeholder text and chip content.
- No real model execution; a stubbed execution hook that logs to the Event/Context layer.

After this zone ships, the next build order is: Left panel (Feature Tree + Mode switch) → Top bar → Status bar → Viewport integration → optional Code editor.
