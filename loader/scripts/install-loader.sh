#!/bin/sh
# Install pinned Mac Museum Mod Loader (BepInEx) into Steam TPM game root.
set -e
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CACHE="$REPO_ROOT/loader/.cache"
ZIP="BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286.zip"
URL="https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789%2B6cea286.zip"
GAME="${TPM_GAME_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum}"
EXPECT_SHA="cc2341544d271f8bcea4e7415acad10766e968d236aaee30cdd79780ec7db00b"

mkdir -p "$CACHE"
cd "$CACHE"
if [ ! -f "$ZIP" ]; then
  curl -fL --retry 3 -o "$ZIP" "$URL"
fi
ACTUAL="$(shasum -a 256 "$ZIP" | awk '{print $1}')"
if [ "$ACTUAL" != "$EXPECT_SHA" ]; then
  echo "SHA256 mismatch: got $ACTUAL want $EXPECT_SHA" 1>&2
  exit 1
fi
rm -rf extract && mkdir extract && unzip -q "$ZIP" -d extract
rsync -a extract/.doorstop_version extract/changelog.txt extract/libdoorstop.dylib extract/run_bepinex.sh extract/BepInEx extract/dotnet "$GAME/"
chmod +x "$GAME/run_bepinex.sh"
sed -i.bak 's|^executable_name=""|executable_name="TPM.app"|' "$GAME/run_bepinex.sh"
rm -f "$GAME/run_bepinex.sh.bak"
# Force Rosetta: pinned package is macos-x64 (CoreCLR/dobby x86_64-only).
python3 - "$GAME/run_bepinex.sh" <<'PY'
import sys
from pathlib import Path
p=Path(sys.argv[1]); t=p.read_text()
t=t.replace('export ARCHPREFERENCE="arm64,x86_64"','export ARCHPREFERENCE="x86_64"')
t=t.replace('exec arch -e DYLD_INSERT_LIBRARIES=','exec arch -x86_64 -e DYLD_INSERT_LIBRARIES=')
# If still bare exec without arch, leave as-is when already patched with -x86_64
p.write_text(t)
print('run_bepinex.sh configured for x86_64')
PY

# Mac .app layout: BepInEx Paths.GameRoot is parent×4 of the exe (folder with TPM.app).
# Data lives under TPM.app/Contents/Resources/Data; GameAssembly under Frameworks.
# Without these links, Preloader gets null gameDataPath / missing GameAssembly.dylib.
cd "$GAME"
ln -sfn "TPM.app/Contents/Resources/Data" "Data"
ln -sfn "TPM.app/Contents/Resources/Data" "Two Point Museum_Data"
ln -sfn "TPM.app/Contents/Frameworks/GameAssembly.dylib" "GameAssembly.dylib"
ln -sfn "TPM.app/Contents/Frameworks/UnityPlayer.dylib" "UnityPlayer.dylib"

# Mac Rosetta: scene-change invoke detour may not fire; delayed Execute loads plugins.
TOOL_DIR="$REPO_ROOT/loader/tools/mac-delayed-execute"
if [ -d "$TOOL_DIR" ]; then
  (cd "$TOOL_DIR" && dotnet build -c Release -v q)
  dotnet "$TOOL_DIR/bin/Release/net10.0/MacDelayedExecute.dll" "$GAME/BepInEx/core/BepInEx.Unity.IL2CPP.dll" || true
fi

# Deploy Host (tpm-mac-loader-host Task 3). Do NOT install capi-probe.
HOST_DLL="$REPO_ROOT/plugins/mac-il2cpp-host/bin/Release/TpmMacIl2CppHost.dll"
PLUGINS="$GAME/BepInEx/plugins"
mkdir -p "$PLUGINS" "$GAME/BepInEx/config" "$GAME/BepInEx/interop"
if [ ! -f "$HOST_DLL" ]; then
  echo "Missing Host build: $HOST_DLL (build plugins/mac-il2cpp-host Release first)" 1>&2
  exit 1
fi
cp -f "$HOST_DLL" "$PLUGINS/"
# Explicitly do not copy plugins/capi-probe/bin/**/*.dll
if [ -f "$PLUGINS/TpmMacCapiProbe.dll" ]; then
  echo "WARN: TpmMacCapiProbe.dll present in plugins; remove to avoid dual GUID load (com.tpm.mac.capi-probe vs com.tpm.mac.il2cpp-host)" 1>&2
fi
echo "Host deployed: $PLUGINS/TpmMacIl2CppHost.dll"
echo "Plugins after install (expect Host; expect NO capi-probe):"
ls -la "$PLUGINS"
echo "Installed Loader into: $GAME"
echo "Launch (Steam running): cd \"$GAME\" && ./run_bepinex.sh"
echo "Log: $GAME/BepInEx/LogOutput.log"
echo "Host (TpmMacIl2CppHost.dll) is deployed by this script; do NOT install capi-probe. Multi-Select is separate."
