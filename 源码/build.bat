@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
"%CSC%" /nologo /target:winexe /win32icon:"%~dp0..\app.ico" /out:"%~dp0..\DeepSeek-Harness-Client.exe" /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" /r:"%~dp0..\Microsoft.Web.WebView2.WinForms.dll" /r:"%~dp0..\Microsoft.Web.WebView2.Core.dll" "%~dp0launcher.cs"
if %errorlevel%==0 (echo [OK] Build succeeded.) else (echo [FAIL] Build failed. See errors above.)
pause
