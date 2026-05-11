# NEXT BLOCK

## Block 1 — AvaloniaApp Minimum Scaffold

**Status:** READY FOR BUILDER
**Priority:** P0 — build is broken, nothing else can proceed

---

### Problem

`Program.cs` compiles against two types that do not exist:

1. `My3DApp.AvaloniaApp.App` (referenced by `AppBuilder.Configure<App>()`)
2. `My3DApp.AvaloniaApp.Services.RuntimeLog` (referenced by exception handlers)

Additionally, the `.csproj` references `Assets\AppIcon.ico` and `app.manifest` which are absent.

The project **cannot build** in its current state.

---

### Goal

Create the minimum set of files that allows `dotnet build` to exit with 0 errors and the app to launch to an empty window.

---

### Files to create

#### `AvaloniaApp/App.axaml`
```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="My3DApp.AvaloniaApp.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

#### `AvaloniaApp/App.axaml.cs`
```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace My3DApp.AvaloniaApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
```

#### `AvaloniaApp/MainWindow.axaml`
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="My3DApp.AvaloniaApp.MainWindow"
        Title="My3DApp"
        Width="1280" Height="800">
  <TextBlock Text="My3DApp — skeleton" HorizontalAlignment="Center" VerticalAlignment="Center"/>
</Window>
```

#### `AvaloniaApp/MainWindow.axaml.cs`
```csharp
using Avalonia.Controls;

namespace My3DApp.AvaloniaApp;

public partial class MainWindow : Window { }
```

#### `AvaloniaApp/Services/RuntimeLog.cs`
```csharp
namespace My3DApp.AvaloniaApp.Services;

public static class RuntimeLog
{
    public static void Write(string context, string message, Exception? ex = null)
    {
        var line = $"[{DateTime.UtcNow:u}] [{context}] {message}";
        if (ex != null) line += $"\n  {ex}";
        System.Diagnostics.Trace.WriteLine(line);
    }
}
```

#### `Assets/AppIcon.ico`
Any valid 16x16 `.ico` file. Use a 1-pixel placeholder or copy any system icon. The file just needs to exist and be a valid ICO so the linker doesn't fail.

#### `app.manifest`
Standard Windows application manifest:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="My3DApp.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

---

### Acceptance criteria

- [ ] `dotnet build` exits 0
- [ ] No missing type errors
- [ ] No missing file errors from `.csproj`
- [ ] App can at minimum launch (even if only tested on Windows)

---

### Must NOT do

- Do not add any NuGet packages
- Do not add any geometry or Engine code
- Do not add any UI views beyond an empty `MainWindow`
- Do not create temp folders
- Do not duplicate `x:Class` directives

---

### Estimated effort

Small — 6 files, ~60 lines of code total.
