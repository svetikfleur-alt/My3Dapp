# UI polish policy

Final UI polish is a rare end-of-cycle stage, not a normal feature-task cleanup step.

Rules:
- Keep at most one pending final UI polish task in TASK_QUEUE.md.
- Run it after functional/structural tasks, not after every feature.
- Polish only existing visible CAD surfaces; do not add new CAD features.
- Preserve viewport behavior, primitives, sketch flow, feature flow, and assistant fallback behavior.
- Build must pass before the task can be marked complete.

Default queue task: Final UI polish pass.
