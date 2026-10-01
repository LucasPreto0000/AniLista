@echo off
setlocal
cd /d "%~dp0source"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /out:..\AniLista.exe /win32icon:..\AniLista.ico /win32manifest:app.manifest /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Net.Http.dll /r:System.Runtime.Serialization.dll /r:System.Web.Extensions.dll AniLista.cs
if errorlevel 1 (echo. & echo Erro ao compilar. Feche o AniLista se estiver aberto e tente de novo. & pause & exit /b 1)
echo. & echo Pronto! AniLista.exe atualizado.
pause
