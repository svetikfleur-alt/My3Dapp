# Template Guide

This is the early guide for adding new maker templates to My3DApp.

## Template goal

A template should generate a useful starter part, not just a visual placeholder.

Good early templates:

- printer brackets
- mounting plates
- standoffs
- fan adapters
- clips
- simple enclosures

## Minimum template contract

Every template should define:

- `id`
- display name
- category
- short description
- parameter list
- validation limits
- command or geometry generation entry point
- export expectation

## Parameter rules

Parameters should have:

- stable names
- default values
- minimum and maximum values
- units
- brief descriptions

Prefer parameters that makers actually edit:

- width
- height
- thickness
- diameter
- spacing
- hole count
- wall thickness

## Early MVP geometry rule

For this MVP, a template may generate:

- command sequences that create geometry in the current part studio
- simplified solids if full detail is not reliable yet

Do not fake support.
If a template is simplified, say so in its notes.

## Export expectation

At least a usable STL export path should work for a template-generated body.

## Suggested folder contents

- `template.md`
- optional future `template.json`
- optional screenshots
- sample parameter presets

## Runtime wiring

Add runtime support in:

- `AvaloniaApp/Services/MakerTemplateLibrary.cs`

Then test:

1. select template in UI
2. edit parameters
3. generate part
4. preview in viewport
5. export STL
