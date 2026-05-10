# Blueprint Execution Protocol

## Purpose

This document defines how the coding agent executes Blueprint growth safely.

## Core rule

Blueprint execution is segment-based.

The agent must never attempt to implement the whole future plan at once.

## Command meaning

When the command `run blueprint` is used, the agent must:

1. find the next unfinished segment in `12_BLUEPRINT_EXPANSION_PLAN.md`
2. implement only that segment
3. stop after that segment
4. report what was changed

## Strong constraints

- Only one segment per run
- No jumping ahead
- No unrelated redesign
- No speculative expansion
- No hidden refactors beyond local necessity

## If conflict appears

If the current segment conflicts with architecture:
- stop
- report conflict
- do not improvise a large redesign silently

## Output format

After execution, the agent should report:

- executed segment ID
- implemented changes
- untouched areas
- risks or conflicts
- suggested next segment
