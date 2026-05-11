' Silent launcher for BuildRunner.ps1 - wraps powershell so no console window flashes.
' Task Scheduler points to this .vbs via wscript.exe; window style 0 = hidden, no flicker.
Dim shell, fso, here, ps1, cmd
Set shell = CreateObject("WScript.Shell")
Set fso   = CreateObject("Scripting.FileSystemObject")
here  = fso.GetParentFolderName(WScript.ScriptFullName)
ps1   = here & "\BuildRunner.ps1"
cmd   = "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & ps1 & """"
shell.Run cmd, 0, False
Set shell = Nothing
Set fso = Nothing
