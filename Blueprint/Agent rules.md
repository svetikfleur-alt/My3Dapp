Create CLAUDE.md file in project root with these rules:

# MY3DAPP — AGENT RULES

## Before every task:
- Run dotnet build first
- Check that build succeeds before making changes
- Read existing code before editing

## Architecture rules:
- Backend files: /Backends/ and /Engine/ only
- UI files: /AvaloniaApp/ only
- Never mix backend and UI in same edit session
- Never create duplicate classes or x:Class directives

## Package rules:
- Do not add new NuGet packages without checking 
  existing ones first
- Do not change WebView2 package version
- Do not add WinForms or WPF references

## After every task:
- Run dotnet build
- Confirm build succeeds before finishing
- Report any warnings

## Never:
- Create temp folders inside /AvaloniaApp/
- Duplicate existing classes
- Run multiple conflicting changes simultaneously
You are causing regressions.

Current problem:
while fixing one area, you remove or degrade other parts that were already working or acceptable.

This is forbidden.

New rule:
- preserve everything that already works
- preserve acceptable existing primitives and usable controls
- do not remove primitives
- do not replace usable UI with text-only crude controls
- do not regress toward old WinForms / utility software feel

The current product must move forward, not sideways or backwards.

Before changing anything:
1. identify what currently works
2. keep it
3. only fix the broken part

If a fix would break an already working part:
- do not apply it blindly
- stop and report the conflict

Current specific regression examples to avoid:
- primitives were removed
- toolbar became text-heavy and old-fashioned
- UI drifted back toward WinForms-like appearance

From now on:
NO REGRESSION.
Preserve working parts unless explicitly instructed otherwise.

ICON / UI CONTROL RULES

The interface must use proper icon-based CAD-like controls, not crude text-only controls.

Requirements:
- use real icon buttons for tools
- use compact grouped tool controls
- use normal modern desktop UI design patterns
- use proper toolbar/button components, not rough text blocks pretending to be tools
- preserve a modern engineering desktop feel

Tool categories that must be icon-based where appropriate:
- sketch tools
- feature tools
- selection/navigation tools
- primitive creation tools
- utility actions

Allowed:
- short labels next to icons only where truly needed
- dropdown labels for feature families if appropriate
- compact text only for secondary metadata or status

Forbidden:
- text-only pseudo-buttons for primary tools
- raw utility-style controls
- fallback old WinForms-looking toolbar
- random mixed text controls instead of designed tool buttons
- crude placeholder controls if acceptable icon/tool controls already exist

Design direction:
- CAD-like
- compact
- icon-first
- modern desktop engineering tool
- not SaaS
- not debug utility
- not old WinForms

If a usable icon-based control already exists, preserve it.
Do not replace existing acceptable tool controls with simpler text-only versions.

Verification:
- primary tools are icon-based or icon-led
- toolbar no longer feels like a text utility strip
- UI did not regress toward old WinForms / debug-tool appearance

Do not use text-only controls for primary CAD tools.
Use proper icon-based toolbar controls and modern desktop UI components.
No visual regression to old WinForms-style utility UI.