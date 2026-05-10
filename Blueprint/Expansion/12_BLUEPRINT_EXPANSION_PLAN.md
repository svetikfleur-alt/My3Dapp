# Blueprint Expansion Plan

This plan grows the system by segments.

Rule:
Each segment must be:
- large enough to matter
- small enough to control
- safe enough to debug

---

## S-01 — Desktop Shell Base
Purpose:
Create a clean desktop shell layout.

Adds:
- top toolbar
- left sidebar
- center viewport container
- right assistant placeholder
- bottom tabs

Does NOT touch:
- CAD logic
- assistant intelligence
- rendering internals

Done when:
- the app reads as an engineering shell
- no dashboard artifacts remain

---

## S-02 — Sketch UI State
Purpose:
Introduce Sketch mode UI state.

Adds:
- sketch-oriented toolbar groups
- sketch mode visual state
- structure for sketch workflow

Does NOT touch:
- 3D feature logic
- assistant intelligence

Done when:
- Sketch mode is visually distinct
- toolbar is appropriate to sketch work

---

## S-03 — 3D UI State
Purpose:
Introduce 3D feature-oriented UI state.

Adds:
- 3D toolbar groups
- 3D mode visual state

Does NOT touch:
- deep geometry engine
- assistant intelligence

Done when:
- 3D mode is visually distinct
- 3D tools are grouped and readable

---

## S-04 — Assistant Panel Shell
Purpose:
Create the structural assistant panel.

Adds:
- assistant layout
- mode selector
- structured response zones

Does NOT touch:
- real assistant intelligence
- deep task logic

Done when:
- assistant panel looks like engineering aid
- not like a generic chat app

---

## S-05 — Model Tree Structure
Purpose:
Introduce proper structure tree behavior.

Adds:
- tree containers
- root items
- structural placeholders

Does NOT touch:
- full dependency system

Done when:
- sidebar reads as model structure, not navigation

---

## S-06 — Basic Context Bridge
Purpose:
Prepare event/context bridge for assistant support.

Adds:
- current mode context
- current selection context
- user action log skeleton

Does NOT touch:
- deep LLM logic

Done when:
- assistant can later be fed structured context

---

## S-07 — Basic Feature Workflow Concept
Purpose:
Introduce minimal sketch-to-feature conceptual chain.

Adds:
- sketch entry point
- feature placeholder flow
- tree progression concept

Does NOT touch:
- advanced geometry internals

Done when:
- the UI and model structure can express a basic feature workflow
