#!/bin/sh
# Remove Mac Museum Mod Loader files from Steam TPM game root (user-side; no Denuvo fight).
set -e
GAME="${TPM_GAME_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum}"
cd "$GAME"
rm -rf BepInEx dotnet
rm -f libdoorstop.dylib run_bepinex.sh .doorstop_version changelog.txt
rm -f Data "Two Point Museum_Data" GameAssembly.dylib UnityPlayer.dylib
rm -f TPM.app/Contents/MacOS/GameAssembly.dylib
rm -f TPM.app/Contents/MacOS/preloader_*.log
rm -f steam_appid.txt
echo "Loader removed from: $GAME"
echo "If the game fails to start after an update, removing these files is the supported user-side recovery."
