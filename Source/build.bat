@echo off
rem Builds ELK.dll against KSP's own managed assemblies plus the shared
rem ToolbarControl install (no SDK or NuGet package required - uses the C#
rem compiler bundled with the .NET Framework). The DLL is written into this
rem repo's GameData/ELK/Plugins, then copied into the KSP install below.
rem Set KSP to your own install path if it differs.

set "KSP=E:\SteamBackup\Kerbal Space Program"
set "MANAGED=%KSP%\KSP_x64_Data\Managed"
set "TOOLBAR=%KSP%\GameData\001_ToolbarControl\Plugins"
set "OUT=%~dp0..\GameData\ELK\Plugins"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

"%CSC%" /noconfig /nostdlib+ /target:library /optimize+ /nologo ^
 /out:"%OUT%\ELK.dll" ^
 /r:"%MANAGED%\mscorlib.dll" ^
 /r:"%MANAGED%\System.dll" ^
 /r:"%MANAGED%\System.Core.dll" ^
 /r:"%MANAGED%\UnityEngine.dll" ^
 /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
 /r:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
 /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
 /r:"%MANAGED%\Assembly-CSharp.dll" ^
 /r:"%TOOLBAR%\ToolbarControl.dll" ^
 "%~dp0ElkBind.cs" ^
 "%~dp0ElkCapture.cs" ^
 "%~dp0ElkConflicts.cs" ^
 "%~dp0ElkConfig.cs" ^
 "%~dp0ElkSlots.cs" ^
 "%~dp0ElkAddon.cs" ^
 "%~dp0ElkToolbarApp.cs" ^
 "%~dp0ElkWindow.cs"

if not %errorlevel%==0 (echo BUILD FAILED & exit /b 1)
copy /y "%OUT%\ELK.dll" "%KSP%\GameData\ELK\Plugins\ELK.dll" >nul
if %errorlevel%==0 (echo OK: ELK.dll built and deployed) else (echo BUILD OK but deploy FAILED - is KSP running?)
