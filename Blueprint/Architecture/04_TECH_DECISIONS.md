# Technical Decisions

## Decision 1: Desktop-first

The product is desktop-first.

Reason:
- local compute
- reduced network dependency
- better alignment with engineering workflows
- better fit for heavy future tasks

## Decision 2: Not browser-first

Browser-first and cloud-first are not the current baseline.

Reason:
- local hardware should be used directly
- internet dependency is undesirable
- the product should remain useful under weak connectivity

## Decision 3: WinForms is not the target shell technology

WinForms may be tolerated as a temporary experiment only.
It is not the target architectural foundation.

Reason:
- poor fit for modern dense engineering UI
- poor long-term fit for custom viewport-heavy studio design
- poor visual and structural ceiling for the intended product

## Decision 4: Modern desktop shell required

The target shell direction is:
- WPF
or
- Avalonia

Final choice may be made later, but the project must be guided as a modern desktop shell project, not a WinForms final architecture.

## Decision 5: Rendering must be separated

Viewport/rendering concerns must be isolated from standard UI controls.

## Decision 6: Assistant is secondary to model logic

Assistant is an augmentation layer.
It must not become a substitute for architecture.

## Decision 7: Organic sculpting postponed

Organic modeling is explicitly postponed to a future stage.

Current focus:
- parametric geometry
- feature workflows
- patterned structures
