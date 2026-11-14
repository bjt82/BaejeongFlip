@echo off
rem 배정플립 빌드 (Windows) - Windows에 기본으로 있는 .NET Framework 4 컴파일러 사용
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist release mkdir release
"%CSC%" /nologo /target:winexe /platform:anycpu /codepage:65001 /r:System.Web.Extensions.dll /r:System.Security.dll /out:release\BaejeongFlip.scr src\*.cs
if errorlevel 1 (echo 빌드 실패 & pause & exit /b 1)
echo 빌드 완료: release\BaejeongFlip.scr
pause
