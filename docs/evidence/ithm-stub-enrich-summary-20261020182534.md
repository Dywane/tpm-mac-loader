# ITHM — managed Il2CppInterop stub enrich (Interop)

**Date:** 2026-10-20 Asia/Shanghai  
**Repo:** `tpm-mac-loader`  
**Stamp:** `20261020182534`

## Root cause (one line)

`AsmResolverDllOutputFormatEmpty` Dummy had TypeDefs **without MethodDefs**, and SanitizeDummy historically `stripAllMethods=true` — Il2CppInterop therefore emitted empty interleaved stubs (`declaredMethods=0`, `NativeMethodInfoPtr*=0`), so MS `AccessTools.Method(OnBecomeActive)` SKIP'd.

## Fix

1. **New** `tools/interop-runtime/EnrichDummyMethods` — inject zero-param method shells from `global-metadata.dat` (names/attrs; void sig) into Dummy before Generate.
2. **SanitizeDummy:** default `stripAllMethods=false`; seed `System.Void` as valuetype; seed `System.Type.op_Equality/op_Inequality/internal_from_handle`.
3. Pipeline: Sanitize → Enrich → Generate (empty unity-libs) → Dedup → deploy `BepInEx/interop/`.

## Offline proof

`docs/evidence/ithm-stub-enrich-reflection-20261020182534.txt`

`TPS.Game.CursorModePlaceItem` after deploy:

- Methods=49, Fields=47, `NativeMethodInfoPtr` count=47
- `OnBecomeActive` / `OnBecomeInactive` / `RestartItemPlaceState` MethodDefs present
- `.cctor` `GetIl2CppMethod(..., "OnBecomeActive", ...)` → `NativeMethodInfoPtr_OnBecomeActive_*`

## Cold-start progression

| Stage | Log |
|-------|-----|
| Pre-fix | `WAITING_FOR_STUB_METHODINFO` + `AccessTools … Could not find … OnBecomeActive` + `declaredMethods=0` |
| After enrich | **`Stub MethodInfo exists but patch threw`** — `MissingMethodException: Type.op_Equality` |
| + op_Equality + Void valuetype | `Stub MethodInfo exists` — `Type.internal_from_handle` missing |
| + internal_from_handle | Harmony progresses; Il2CppInterop HarmonySupport `DobbyDetour Preparing` on OnBecomeActive native fn (same crash surface as banned native detour) |

Key excerpts: `ithm-stub-enrich-cold-*-20261020182534-*.txt/log`

**AccessTools no longer SKIPs OnBecomeActive** (MethodInfo present). Full `PLACEMENT_READY via=managed Harmony` not yet green — remaining HarmonySupport / OnBecomeActive native stability (out of stub MethodInfo scope).

## Deploy for 建造

```bash
# from tpm-mac-loader tip
cp docs/evidence/il2cppinterop-out-deduped-20261020182534/*.dll \
  "$GAME/BepInEx/interop/"
# already deployed on this machine; backup at BepInEx/interop.bak-pre-enrich-20261020182534
```

MS should log MethodInfo found (not WAITING_FOR_STUB). Then 建造 can wire Harmony Prefix; expect HarmonySupportGate / Dobby on enter methods as next wall.

## Remaining risks

- Enrich v1 only zero-param as **void** — getters wrongly void → `Unable to find method Type::get_*` noise; need binary MetaReg for real signatures or getter allowlist skip (already skip get_/set_ except priority).
- `Il2CppSystem.Type.op_Equality` / `internal_from_handle` seeded; other HarmonySupport Type APIs may still be incomplete.
- Managed Harmony on `OnBecomeActive` still ends in **Dobby native detour** (Il2CppInterop.HarmonySupport) — historically crashy; may need alternate enter MethodInfo or HarmonySupportGate.
- Do **not** merge Host 0.2.8; keep thin Host 0.4.x.
