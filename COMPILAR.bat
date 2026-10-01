@echo off
setlocal
cd /d "%~dp0"
set ANILISTA_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%ANILISTA_CSC%" set ANILISTA_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%ANILISTA_CSC%" /nologo /target:winexe /optimize+ /out:AniLista.exe /win32icon:AniLista.ico /win32manifest:source\app.manifest /resource:source\assets\cursors\arrow.cur,AniLista.Cursors.arrow.cur /resource:source\assets\cursors\hand.cur,AniLista.Cursors.hand.cur /resource:source\assets\cursors\ibeam.cur,AniLista.Cursors.ibeam.cur /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Net.Http.dll /r:System.Runtime.Serialization.dll /r:System.Web.Extensions.dll source\*.cs
if errorlevel 1 (echo. & echo Erro ao compilar. Feche o AniLista se estiver aberto e tente de novo. & pause & exit /b 1)
echo. & echo Pronto! AniLista.exe atualizado.
pause
