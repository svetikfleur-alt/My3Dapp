# Contributing to My3DApp

Thank you for your interest in contributing! My3DApp is an open-source AI-assisted maker CAD studio built on .NET 10 and Avalonia UI.

## Ways to contribute

- **Bug reports** — open a GitHub issue with steps to reproduce, expected vs. actual behavior, and your OS/build version
- **Feature requests** — open an issue describing the use case before starting work
- **Templates** — add new parametric maker templates to `PartLibrary/Templates/`; see [TEMPLATE_GUIDE.md](PartLibrary/TEMPLATE_GUIDE.md)
- **Code** — fix bugs, improve geometry builders, extend the sketch engine, or improve the UI

## Development setup

Requirements: Windows 11, .NET 10 SDK, Visual Studio 2022 / Rider / VS Code with C# Dev Kit.

```
git clone https://github.com/your-org/My3DApp.git
cd My3DApp
dotnet build
dotnet run
```

## Project layout

| Path | Purpose |
|---|---|
| `AvaloniaApp/` | All UI — XAML, ViewModels, code-behind |
| `AvaloniaApp/Services/` | App-level services (workspace controller, autosave, settings) |
| `AvaloniaApp/ViewModels/` | MVVM ViewModels |
| `AvaloniaApp/Dialogs/` | Modal dialog windows |
| `Backends/` | CAD computation backends |
| `Engine/` | FormaCore geometry engine |
| `PartLibrary/Templates/` | Parametric template manifests |
| `Assets/Icons/` | SVG icon set |

**Architecture rules** (see [CLAUDE.md](CLAUDE.md)):
- Backend code belongs in `/Backends/` and `/Engine/` only
- UI code belongs in `/AvaloniaApp/` only
- Never create duplicate classes or `x:Class` directives

## Adding a template

1. Create `PartLibrary/Templates/<YourTemplate>/`
2. Add `template.json` (see [TEMPLATE_GUIDE.md](PartLibrary/TEMPLATE_GUIDE.md))
3. Add `template.md` with parameter table and geometry notes
4. Register a builder in `AvaloniaApp/Services/MakerTemplateLibrary.cs`
5. Run `dotnet build` and verify the template appears in the Templates tab

## Pull request checklist

- [ ] `dotnet build` passes with 0 errors, 0 warnings
- [ ] No new NuGet packages added without discussion
- [ ] No WinForms/WPF references added
- [ ] UI changes tested in the running app
- [ ] Template JSON validated against schema in TEMPLATE_GUIDE.md
- [ ] PR description explains the why, not just the what

## Code style

- C# 13 / .NET 10 idioms; prefer `var`, collection expressions `[]`, primary constructors
- No comments unless the reason is non-obvious
- MVVM: ViewModel raises `PropertyChanged`; no business logic in code-behind
- Keep methods short and focused

## License

MIT — see [LICENSE](LICENSE).
