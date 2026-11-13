@echo off
setlocal
title 배정플립 1.0 제거
set "DEST=%LOCALAPPDATA%\BaejeongFlip"

if exist "%DEST%\BaejeongFlip.scr" start "" /wait "%DEST%\BaejeongFlip.scr" /uninstall
if exist "%DEST%" rmdir /s /q "%DEST%"
if exist "%LOCALAPPDATA%\FlipClock" rmdir /s /q "%LOCALAPPDATA%\FlipClock"

echo 배정플립 화면보호기를 제거했습니다.
pause
endlocal
