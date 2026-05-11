# Implementation Rules

## General rule

The coding agent must implement structure before polish.

## Must do

- respect Blueprint
- keep UI modular
- keep viewport dominant
- keep modes separate
- keep assistant secondary to model architecture
- prefer small safe changes

## Must not do

- do not generate fake working features
- do not create placeholder buttons that look implemented
- do not redesign the entire app without need
- do not introduce dashboard patterns
- do not introduce browser-style UI
- do not overload the toolbar
- do not mix Sketch and 3D tools in one undifferentiated block
- do not silently expand scope
- do not create unnecessary file explosion

## Code organization principle

UI, model logic, rendering, and assistant concerns must stay separated.

## Increment principle

Implement one clear unit of progress at a time.

## Failure principle

If a requested change conflicts with architecture, stop and report instead of improvising.
