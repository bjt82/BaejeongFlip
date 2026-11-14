#!/usr/bin/env bash
# 배정플립 빌드 (Linux / Mono) — .NET Framework 4.8 기준 어셈블리로 컴파일해서 Windows에서 그대로 동작하게 함
# 사용법: ./build.sh            → release/BaejeongFlip.scr
#         ./build.sh zip        → release/BaejeongFlip-1.0.zip (설치 스크립트 포함)
#         ./build.sh setup      → release/BaejeongFlip-1.0-Setup.exe (설치 마법사, NSIS 필요)
# 인증키를 함께 묶으려면 neis-key.txt(NEIS), kma-key.txt(기상청) 를 이 폴더에 두세요 (git 에는 올라가지 않음).
#   zip   : 인증키가 zip 안에 들어감
#   setup : 공개용과 함께 BaejeongFlip-1.0-학교용-Setup.exe 도 만듦
set -euo pipefail
cd "$(dirname "$0")"
VER=$(sed -n 's/.*AssemblyInformationalVersion("\([^"]*\)").*/\1/p' src/BaejeongFlip.cs)
REFDIR=.build/net48ref
if [ ! -f "$REFDIR/build/.NETFramework/v4.8/mscorlib.dll" ]; then
  mkdir -p "$REFDIR"
  curl -sSL -o "$REFDIR/ref.nupkg" "https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3"
  (cd "$REFDIR" && unzip -q -o ref.nupkg)
fi
R="$REFDIR/build/.NETFramework/v4.8"
mkdir -p release
mcs -target:winexe -platform:anycpu -codepage:utf8 -nostdlib -noconfig \
  -r:"$R/mscorlib.dll" -r:"$R/System.dll" -r:"$R/System.Core.dll" -r:"$R/System.Drawing.dll" \
  -r:"$R/System.Windows.Forms.dll" -r:"$R/System.Web.Extensions.dll" -r:"$R/System.Security.dll" \
  -out:release/BaejeongFlip.scr src/*.cs
echo "빌드 완료: release/BaejeongFlip.scr"
if [ "${1:-}" = "zip" ]; then
  python3 - "$VER" <<'PY'
import sys, zipfile, os
ver = sys.argv[1]
out = f"release/BaejeongFlip-{ver}.zip"
files = [("release/BaejeongFlip.scr", "BaejeongFlip.scr"),
         ("installer/설치하기.bat", "설치하기.bat"),
         ("installer/제거하기.bat", "제거하기.bat"),
         ("installer/설치방법.txt", "설치방법.txt")]
for kf in ("neis-key.txt", "kma-key.txt"):
    if os.path.exists(kf):
        files.append((kf, kf))
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for src, name in files:
        z.write(src, name)
keys = [k for k in ("neis-key.txt", "kma-key.txt") if os.path.exists(k)]
print("패키지 완료:", out, "(인증키 포함: " + ", ".join(keys) + ")" if keys else "(인증키 없음)")
PY
fi
if [ "${1:-}" = "setup" ]; then
  command -v makensis >/dev/null || { echo "makensis 가 없습니다 (Ubuntu: sudo apt install nsis)"; exit 1; }
  export LANG=C.UTF-8 LC_ALL=C.UTF-8
  makensis -V2 -DVERSION="$VER" -DSCR="$PWD/release/BaejeongFlip.scr" \
    -DOUTFILE="$PWD/release/BaejeongFlip-$VER-Setup.exe" installer/setup.nsi
  echo "설치 파일: release/BaejeongFlip-$VER-Setup.exe (공개용, 인증키 없음)"
  if [ -f neis-key.txt ] || [ -f kma-key.txt ]; then
    KEYS=()
    [ -f neis-key.txt ] && KEYS+=(-DKEYFILE="$PWD/neis-key.txt")
    [ -f kma-key.txt ] && KEYS+=(-DKMAKEYFILE="$PWD/kma-key.txt")
    makensis -V2 -DVERSION="$VER" -DSCR="$PWD/release/BaejeongFlip.scr" "${KEYS[@]}" \
      -DOUTFILE="$PWD/release/BaejeongFlip-$VER-학교용-Setup.exe" installer/setup.nsi
    echo "설치 파일: release/BaejeongFlip-$VER-학교용-Setup.exe (인증키 포함, 외부 공개 금지)"
  fi
fi
