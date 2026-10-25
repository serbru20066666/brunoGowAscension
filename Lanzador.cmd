@echo off
rem Lanzador de God of War: Ascension con los ajustes (resolucion, FSR, pantalla, idioma...).
start "" powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0launcher\lanzador.ps1"
