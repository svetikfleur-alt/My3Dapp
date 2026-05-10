// Forwarding shim — the real implementation lives in My3DApp.Core.RuntimeLog
// so Engine and Backends don't depend on the UI layer.
global using RuntimeLog = My3DApp.Core.RuntimeLog;
