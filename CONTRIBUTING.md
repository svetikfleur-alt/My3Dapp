# Contributing to UMX1 Studio

> **Early work in progress. Not production CAD yet.**

UMX1 Studio is an open-source AI-assisted maker CAD app. Contributions are welcome, especially focused, verifiable improvements.

## Good first contributions

- Add a new maker template under `PartLibrary/Templates/`
- Improve template validation or parameter UX
- Improve STL/OBJ export reliability
- Improve sketch interactions, constraints, or dimensions
- Improve docs and demo flows
- Add tests around template generation and document persistence

## Contributor workflow

1. Fork and clone the repo
2. Build locally:

```powershell
dotnet build .\My3DApp.csproj -c Debug -p:StudioUiHost=Avalonia
```

3. Run the app and verify the changed flow manually
4. Keep changes focused and avoid regressions
5. Open a PR with:
   - what changed
   - how you tested it
   - known limitations

## AI-assisted contributions

Have GPT Plus / Codex / Claude? That’s welcome here.

Useful contribution prompts:
- add a new maker template
- improve a template manifest and matching builder
- tighten document save/recovery flow
- improve sketch or export reliability

Please still test the generated output before submitting a PR.

## Rules

- Preserve working functionality
- Do not replace good icon-based controls with crude text-only UI
- Do not add fake features that only look implemented
- Prefer small, verifiable increments
- Run a build before and after significant changes
