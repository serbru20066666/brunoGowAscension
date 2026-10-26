@echo off
rem Compila GowAscension.exe (el lanzador) en la raiz del repositorio con el compilador de C#
rem que trae Windows (.NET Framework 4.8). No hace falta instalar nada.
setlocal
set "RAIZ=%~dp0..\.."
rem primer argumento opcional: otra ruta de salida
set "SALIDA=%~1"
if "%SALIDA%"=="" set "SALIDA=%RAIZ%\GowAscension.exe"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FW%\WPF"
if not exist "%FW%\csc.exe" (echo No encuentro csc.exe en %FW% & exit /b 1)
set "ICONO="
if exist "%~dp0icono.ico" set "ICONO=/win32icon:%~dp0icono.ico"
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /out:"%SALIDA%" %ICONO% ^
  /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" ^
  /r:"%FW%\System.Xaml.dll" /r:"%FW%\System.Windows.Forms.dll" /r:System.dll /r:System.Core.dll ^
  "%~dp0Lanzador.cs"
exit /b %ERRORLEVEL%
