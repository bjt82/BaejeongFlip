#!/usr/bin/env bash
# 배정플립 빌드 (Linux / Mono) — .NET Framework 4.8 기준 어셈블리로 컴파일해서 Windows에서 그대로 동작하게 함
# 사용법: ./build.sh            → release/BaejeongFlip.scr
#         ./build.sh zip        → release/BaejeongFlip-1.0.zip (설치 스크립트 포함)
# 인증키를 함께 묶으려면 neis-key.txt 를 이 폴더에 두세요 (git 에는 올라가지 않음).
set -euo pipefail
cd "$(dirname "$0")"
VER=1.0
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
  -out:release/BaejeongFlip.scr src/BaejeongFlip.cs src/Info.cs
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
if os.path.exists("neis-key.txt"):
    files.append(("neis-key.txt", "neis-key.txt"))
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for src, name in files:
        z.write(src, name)
print("패키지 완료:", out, "(인증키 포함)" if os.path.exists("neis-key.txt") else "(인증키 없음)")
PY
fi
