; 배정플립 설치 파일 (NSIS 3, 유니코드)
; 공개용:  makensis -DVERSION=1.0 -DSCR=<BaejeongFlip.scr> -DOUTFILE=<출력.exe> installer/setup.nsi
; 학교용:  위 명령에 -DKEYFILE=<neis-key.txt> -DKMAKEYFILE=<kma-key.txt> 추가 → 인증키가 설치 파일 안에 들어감 (외부 공개 금지)
; build.sh setup 으로 두 가지를 한 번에 만들 수 있습니다.

Unicode true
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!ifndef VERSION
  !define VERSION "1.0"
!endif
!ifndef SCR
  !error "SCR (BaejeongFlip.scr 경로)를 -DSCR=... 로 지정하세요."
!endif
!ifndef OUTFILE
  !define OUTFILE "BaejeongFlip-${VERSION}-Setup.exe"
!endif

!define APPNAME   "배정플립"
!define APPKEY    "BaejeongFlip"
!define PUBLISHER "배정고등학교 교육연구부"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPKEY}"

!ifdef KEYFILE
  Name "${APPNAME} ${VERSION} (학교용)"
!else
  Name "${APPNAME} ${VERSION}"
!endif
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\${APPKEY}"
RequestExecutionLevel user          ; 관리자 권한 없이 사용자 폴더에 설치
SetCompressor /SOLID lzma
BrandingText "${PUBLISHER}"
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${VERSION}.0.0"
VIAddVersionKey /LANG=1042 "ProductName" "${APPNAME}"
VIAddVersionKey /LANG=1042 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1042 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1042 "FileDescription" "${APPNAME} ${VERSION} 설치"
VIAddVersionKey /LANG=1042 "CompanyName" "${PUBLISHER}"
VIAddVersionKey /LANG=1042 "LegalCopyright" "MIT License, ${PUBLISHER}"

Var Minutes
Var TimeoutList
Var IsUpdate          ; "/S /UPDATE" 로 실행되면 1: 설정 창의 [업데이트] 버튼이 호출하는 조용한 업데이트 모드

; ── 화면 ──
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${APPNAME} ${VERSION} 설치"
!ifdef KEYFILE
  !define MUI_WELCOMEPAGE_TEXT "배정고등학교 플립시계 화면보호기를 설치합니다.$\r$\n$\r$\n시계, 날짜, 학사일정, 급식, 날씨를 보여 줍니다. 학교 정보(NEIS)와 기상청 인증키가 함께 설치되므로 따로 입력할 필요가 없습니다.$\r$\n$\r$\n관리자 권한 없이 이 사용자 계정에만 설치됩니다."
!else
  !define MUI_WELCOMEPAGE_TEXT "배정고등학교 플립시계 화면보호기를 설치합니다.$\r$\n$\r$\n시계, 날짜, 학사일정, 급식, 날씨를 보여 줍니다. 학교 정보(NEIS) 인증키는 설치 후 화면보호기 설정 창에서 넣을 수 있습니다.$\r$\n$\r$\n관리자 권한 없이 이 사용자 계정에만 설치됩니다."
!endif
!insertmacro MUI_PAGE_WELCOME
Page custom TimeoutPage TimeoutLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "설치 완료"
!define MUI_FINISHPAGE_TEXT "${APPNAME}이(가) 화면보호기로 지정되었습니다.$\r$\n$Minutes분 동안 입력이 없으면 시작됩니다."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "지금 실행해 보기"
!define MUI_FINISHPAGE_RUN_FUNCTION RunNow
!define MUI_FINISHPAGE_RUN_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME
!define MUI_FINISHPAGE_SHOWREADME_TEXT "화면보호기 설정 창 열기 (표시 항목 바꾸기)"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION OpenSettings
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Korean"

; ── 대기 시간 고르기 ──
Function .onInit
  StrCpy $Minutes "5"
  StrCpy $IsUpdate "0"
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/UPDATE" $R1
  ${IfNot} ${Errors}
    StrCpy $IsUpdate "1"
  ${EndIf}
FunctionEnd

Function TimeoutPage
  !insertmacro MUI_HEADER_TEXT "화면보호기 대기 시간" "몇 분 동안 입력이 없으면 배정플립을 시작할까요?"
  nsDialogs::Create 1018
  Pop $0
  ${NSD_CreateLabel} 0 0 100% 24u "대기 시간"
  Pop $0
  ${NSD_CreateDropList} 0 26u 90u 80u ""
  Pop $TimeoutList
  ${NSD_CB_AddString} $TimeoutList "1분"
  ${NSD_CB_AddString} $TimeoutList "3분"
  ${NSD_CB_AddString} $TimeoutList "5분"
  ${NSD_CB_AddString} $TimeoutList "10분"
  ${NSD_CB_AddString} $TimeoutList "15분"
  ${NSD_CB_AddString} $TimeoutList "30분"
  ${NSD_CB_SelectString} $TimeoutList "$Minutes분"
  ${NSD_CreateLabel} 0 50u 100% 36u "나중에 Windows 화면보호기 설정에서 언제든 바꿀 수 있습니다."
  Pop $0
  nsDialogs::Show
FunctionEnd

Function TimeoutLeave
  ${NSD_GetText} $TimeoutList $0
  ${If} $0 == "1분"
    StrCpy $Minutes "1"
  ${ElseIf} $0 == "3분"
    StrCpy $Minutes "3"
  ${ElseIf} $0 == "10분"
    StrCpy $Minutes "10"
  ${ElseIf} $0 == "15분"
    StrCpy $Minutes "15"
  ${ElseIf} $0 == "30분"
    StrCpy $Minutes "30"
  ${Else}
    StrCpy $Minutes "5"
  ${EndIf}
FunctionEnd

; ── 설치 ──
Section "Install"
  SetOutPath "$INSTDIR"

  ; 업데이트: 실행 중인 파일도 이름은 바꿀 수 있으므로, 이전 파일을 .bak 으로 옮겨 두고 새 파일을 넣는다 (실패 시 되돌림용)
  ${If} $IsUpdate == "1"
    Delete "$INSTDIR\BaejeongFlip.scr.bak"
    ClearErrors
    Rename "$INSTDIR\BaejeongFlip.scr" "$INSTDIR\BaejeongFlip.scr.bak"
    ${If} ${Errors}
      Delete "$INSTDIR\BaejeongFlip.scr.bak2"
      Rename "$INSTDIR\BaejeongFlip.scr" "$INSTDIR\BaejeongFlip.scr.bak2"
    ${EndIf}
  ${EndIf}

  File "/oname=BaejeongFlip.scr" "${SCR}"

  ; 업데이트: 새 파일이 제대로 도는지 시험 → 실패하면 이전 파일로 되돌리고 중단
  ${If} $IsUpdate == "1"
    ExecWait '"$INSTDIR\BaejeongFlip.scr" /selftest' $0
    ${If} $0 != 0
      Delete "$INSTDIR\BaejeongFlip.scr"
      Rename "$INSTDIR\BaejeongFlip.scr.bak" "$INSTDIR\BaejeongFlip.scr"
      Rename "$INSTDIR\BaejeongFlip.scr.bak2" "$INSTDIR\BaejeongFlip.scr"
      SetErrorLevel 3
      Abort
    ${EndIf}
  ${EndIf}
!ifdef KEYFILE
  File "/oname=neis-key.txt" "${KEYFILE}"
!endif
!ifdef KMAKEYFILE
  File "/oname=kma-key.txt" "${KMAKEYFILE}"
!endif

  ; 예전 이름(플립시계)으로 설치된 파일 정리
  RMDir /r "$LOCALAPPDATA\FlipClock"

  ; 화면보호기로 지정하고 대기 시간 적용
  ; (업데이트 때는 이미 지정된 화면보호기·대기 시간을 그대로 두고, 지정이 풀려 있을 때만 다시 지정)
  ${If} $IsUpdate == "1"
    ReadRegStr $3 HKCU "Control Panel\Desktop" "SCRNSAVE.EXE"
    ${If} $3 == "$INSTDIR\BaejeongFlip.scr"
      ; 그대로 둠
    ${Else}
      ReadRegStr $4 HKCU "Control Panel\Desktop" "ScreenSaveTimeOut"
      ${If} $4 == ""
        StrCpy $4 "300"
      ${EndIf}
      ExecWait '"$INSTDIR\BaejeongFlip.scr" /install $4'
    ${EndIf}
  ${Else}
    IntOp $1 $Minutes * 60
    ExecWait '"$INSTDIR\BaejeongFlip.scr" /install $1'
  ${EndIf}

  ; 인증키 파일을 바로 암호화 저장(파일은 자동 삭제)하고, 일정·급식·날씨를 미리 받아 둠
  Exec '"$INSTDIR\BaejeongFlip.scr" /fetch'

  ; Windows "설정 → 앱"에 등록
  WriteUninstaller "$INSTDIR\uninstall.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "URLInfoAbout" "https://github.com/bjt82/BaejeongFlip"
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" 200

  ; 학교·회사 정책으로 화면보호기가 고정된 PC 안내
  ReadRegStr $2 HKCU "Software\Policies\Microsoft\Windows\Control Panel\Desktop" "SCRNSAVE.EXE"
  ${If} $2 != ""
    MessageBox MB_ICONEXCLAMATION|MB_OK "이 PC는 관리자 정책으로 화면보호기가 고정되어 있습니다.$\r$\n설치는 되었지만 정책 때문에 배정플립이 쓰이지 않을 수 있습니다." /SD IDOK
  ${EndIf}

  ; 업데이트가 끝나면 새 버전의 설정 창을 다시 열어 줌 (왼쪽 아래 문구로 새 버전 확인)
  ${If} $IsUpdate == "1"
    Exec '"$INSTDIR\BaejeongFlip.scr" /c'
  ${EndIf}
SectionEnd

Function RunNow
  Exec '"$INSTDIR\BaejeongFlip.scr" /s'
FunctionEnd

Function OpenSettings
  Exec '"$SYSDIR\control.exe" desk.cpl,,@screensaver'
FunctionEnd

; ── 제거 ──
Section "Uninstall"
  ExecWait '"$INSTDIR\BaejeongFlip.scr" /uninstall'
  Delete "$INSTDIR\BaejeongFlip.scr"
  Delete "$INSTDIR\BaejeongFlip.scr.bak"
  Delete "$INSTDIR\BaejeongFlip.scr.bak2"
  Delete "$INSTDIR\neis-key.txt"
  Delete "$INSTDIR\info-cache.json"
  Delete "$INSTDIR\uninstall.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTKEY}"
SectionEnd
