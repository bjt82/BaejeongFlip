@echo off
setlocal
title 배정플립 설치

set "SRC=%~dp0BaejeongFlip.scr"
set "DEST=%LOCALAPPDATA%\BaejeongFlip"
set "SCR=%DEST%\BaejeongFlip.scr"

if not exist "%SRC%" (
  echo [오류] 이 파일과 같은 폴더에 BaejeongFlip.scr 가 없습니다.
  echo zip 파일을 먼저 "압축 풀기" 한 뒤 다시 실행하세요.
  pause
  exit /b 1
)

echo.
echo  배정플립 화면보호기를 설치합니다.
echo  설치 위치: %DEST%
echo.
set "MIN=5"
set /p "MIN= 몇 분 동안 입력이 없으면 시작할까요? (그냥 Enter = 5분): "
set "SEC=300"
set /a SEC=MIN*60 >nul 2>&1
if %SEC% LSS 60 set "SEC=300"
set /a MIN=SEC/60

if not exist "%DEST%" mkdir "%DEST%"
copy /y "%SRC%" "%SCR%" >nul
if errorlevel 1 (
  echo [오류] 파일을 복사하지 못했습니다. 화면보호기가 실행 중이면 끄고 다시 시도하세요.
  pause
  exit /b 1
)

rem NEIS 인증키 파일이 함께 있으면 복사 (설정 창의 인증키 칸이 비어 있을 때 사용)
if exist "%~dp0neis-key.txt" copy /y "%~dp0neis-key.txt" "%DEST%\neis-key.txt" >nul

rem 예전 이름(플립시계, FlipClock)으로 설치된 파일 정리
if exist "%LOCALAPPDATA%\FlipClock" rmdir /s /q "%LOCALAPPDATA%\FlipClock" >nul 2>&1

rem 인터넷에서 받은 파일 표시(차단) 해제
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath $env:SCR" >nul 2>&1

rem 화면보호기로 지정하고 바로 적용 (레지스트리 + 시스템 설정)
start "" /wait "%SCR%" /install %SEC%

rem 회사/학교 PC 그룹 정책이 화면보호기를 고정했는지 확인
reg query "HKCU\Software\Policies\Microsoft\Windows\Control Panel\Desktop" /v SCRNSAVE.EXE >nul 2>&1
if not errorlevel 1 (
  echo.
  echo [주의] 이 PC는 관리자 정책으로 화면보호기가 고정되어 있습니다.
  echo        설치는 되었지만 정책 때문에 배정플립이 쓰이지 않을 수 있습니다.
)

echo.
echo  설치 완료: %MIN%분 동안 입력이 없으면 배정플립이 켜집니다.
echo  화면보호기 설정 창을 엽니다. "설정" 버튼으로 표시 항목을 바꿀 수 있습니다.
echo.
start "" control desk.cpl,,@screensaver

set "T=N"
set /p "T= 지금 바로 실행해서 확인해 볼까요? (Y/N, 그냥 Enter = N): "
if /i "%T%"=="Y" start "" "%SCR%" /s
endlocal
