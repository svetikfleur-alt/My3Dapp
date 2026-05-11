# Third-Party Integration Policy

This project may use outside references to accelerate development, but we do it in a way that keeps the app shippable and the provenance clear.

## Current reference status

- `References/zoo/` and the other `References/*` folders are design/reference material.
- They are not compiled into the app.
- `LEAP71_ShapeKernel-main.zip` is present locally as a reference archive.

## What is allowed

### 1. Visual and behavioral reference

We may study outside products and examples for:

- layout structure
- interaction flow
- feature sequencing
- geometry concepts
- dialog patterns
- tree organization
- terminology at a high level

This is the default and preferred path.

### 2. Clean-room reimplementation

We may read outside code or examples, then implement the same idea in our own architecture using:

- our own types
- our own naming
- our own control flow
- our own UI structure

This is the safest way to move fast without dragging license obligations deep into the product.

### 3. Direct inclusion only when explicitly intentional

If we intentionally import third-party source, it must be:

- isolated
- traceable
- license-documented
- kept separate from first-party files as much as practical

Do not silently blend imported code into `AvaloniaApp/` or `Engine/` as if it were original project code.

## What is not allowed

- blind copy-paste from reference repos into product files
- line-for-line porting without attribution and license review
- mixing third-party example files into first-party code with no provenance
- copying branded text, screenshots, or product-specific marketing content into the shipped UI
- using reference geometry/demo assets as if they were original project assets

## Source-specific guidance

### Zoo / KittyCAD

The public `KittyCAD/modeling-app` repo is published under MIT license.

Safe use:

- study UI structure
- study interaction patterns
- study code/editor/assistant layout ideas
- reimplement concepts in our own Avalonia app

Avoid:

- copying branded wording such as Zoo/Zookeeper specific UI text
- cloning exact component structure unless we intentionally vendor code and preserve MIT notices
- copying KCL-specific code/editor behavior as if it were our own language/runtime

### LEAP71 / ShapeKernel / PicoGK

The local `LEAP71_ShapeKernel-main.zip` contains an Apache 2.0 license.

Safe use:

- study geometry layering ideas
- study example decomposition
- study primitive and computational geometry patterns
- re-express algorithms in our own CAD core

If directly importing Apache 2.0 code:

- keep the original license text
- retain notices
- mark modified files clearly
- document which files came from where

## Required process for any future third-party code import

If we ever import actual source files, do all of the following:

1. Put imported material under `ThirdParty/<source-name>/`.
2. Keep the original `LICENSE` with it.
3. Add `ThirdParty/<source-name>/SOURCE.md` with:
   - upstream URL
   - upstream revision or release
   - license
   - what we imported
   - what we changed
4. Add a short note in this file describing where that code is used.
5. Do not move imported files into first-party folders unless there is a strong reason and provenance stays visible.

## Practical rule for this repo

For this project, the preferred path is:

1. use references to understand the target behavior
2. write a concise internal spec in our own words
3. implement inside our own architecture
4. cite inspiration in docs or commit notes when useful

That gives us most of the speed benefit without contaminating the app with mixed-origin source.
