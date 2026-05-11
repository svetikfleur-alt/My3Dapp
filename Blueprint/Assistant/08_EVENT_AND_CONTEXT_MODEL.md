# Event and Context Model

## Purpose

The assistant cannot reason well without context.
The system must expose structured context.

## Context sources

- current mode
- selected object / feature
- selected plane
- active tool
- recent user actions
- model tree state
- current task/prompt

## Example action log entries

- Enter Sketch on Top Plane
- Draw Circle
- Set Diameter = 20mm
- Exit Sketch
- Select Extrude

## Why this exists

This layer allows future assistant behaviors such as:
- intent suggestion
- next-step assistance
- think-and-do workflows
- error explanation
- action recovery

## Rule

Assistant reasoning must be context-fed, not hallucination-led.
