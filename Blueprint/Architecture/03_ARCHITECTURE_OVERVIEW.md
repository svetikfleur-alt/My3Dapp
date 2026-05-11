# Architecture Overview

## High-level architecture

The system must be separated into layers.

### Layer 1: UI Shell
Contains:
- top toolbar
- left sidebar
- center viewport container
- right assistant panel
- bottom tabs

This layer manages interface structure, not geometry.

### Layer 2: Viewport / Rendering Layer
Contains:
- viewport rendering area
- axes / grid / planes visualization
- future camera/navigation integration

This layer is visual and interactive, but not the owner of product logic.

### Layer 3: Model / Feature Layer
Contains:
- sketches
- features
- parameters
- model tree
- feature history / graph

This layer is the source of truth for modeling state.

### Layer 4: Assistant Layer
Contains:
- assistant modes
- prompt/task handling
- structured responses
- future intent support
- future action suggestions

This layer must not directly become the source of truth for geometry.

### Layer 5: Event / Context Layer
Contains:
- user actions
- selection state
- current mode
- current task
- current feature context

This layer feeds assistant reasoning.

## Architecture principle

UI must not directly own modeling truth.
Model data must not be embedded inside visual widgets.
Assistant must react to context, not replace product architecture.
