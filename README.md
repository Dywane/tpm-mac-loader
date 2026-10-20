# tpm-mac-loader

Mac-native TPM loader + thin Il2CppInterop Host for *The Planet of the Machines* (Unity IL2CPP).

## Contents

- `loader/` — BepInEx Unity.IL2CPP macOS install/uninstall scripts and version pin
- `plugins/mac-il2cpp-host/` — thin Host plugin (Interop runtime kernel + shared Harmony)
- `tools/` — interop-runtime generators and RE helpers
- `docs/` — specs, plans, gates, research for loader/host/interop

## Companion repo

Gameplay / Multiselect BepInEx plugin lives in **[tpm-mac-multiselect](https://github.com/Dywane/tpm-mac-multiselect)** (`HardDependency` on this Host).

## Provenance

Initial import from monorepo worktree `TPM-muti-select` branch `track-a/tps-game-loaderrs` tip `3668dca`.
Probes (`capi-probe`, `interop-probe`) and `docs/evidence/` binary dumps were excluded.
