# MY3DAPP - AGENT RULES

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
