@echo off
rem God of War: Ascension -- 2560x1440 interno escalado a 4K con FSR 1, pantalla completa, en espanol.
rem Alt+Intro o F11 alternan ventana / pantalla completa.
setlocal
if not defined GOWA_RES_SCALE set "GOWA_RES_SCALE=2"
if not defined GOWA_FSR set "GOWA_FSR=1"
if not defined GOWA_FULLSCREEN set "GOWA_FULLSCREEN=1"
if not defined GOWA_LANG set "GOWA_LANG=3"
rem medidor en pantalla (FPS, resolucion, FG); F10 lo muestra u oculta
if not defined GOWA_OSD set "GOWA_OSD=1"
call "%~dp0jogar.cmd"
