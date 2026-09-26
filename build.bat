@echo off
cd /d "%~dp0src"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set RES=
for %%f in (sample\*) do call set RES=%%RES%% "-resource:%%f,%%~nxf"
"%CSC%" -nologo -codepage:65001 -target:winexe -win32icon:icon.ico -out:..\CursorHub.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Core.dll %RES% Program.cs
