using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FormaCore.Engine.Exact;

/// <summary>
/// Ensures the Windows loader can resolve the native OCCT DLLs (TK*.dll, freetype…)
/// that sit next to the managed output. Hosts like testhost.exe run from elsewhere,
/// so the app base directory must be added to the native DLL search path BEFORE the
/// mixed-mode My3DApp.Occt.dll is loaded.
/// </summary>
internal static class OcctNativeBootstrap
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectoryW(string lpPathName);

    [ModuleInitializer]
    internal static void Ensure() => SetDllDirectoryW(AppContext.BaseDirectory);
}
