# tools/interop-runtime (轨 A)

Offline helpers: Dummy sanitize → Il2CppInterop generate → deploy `BepInEx/interop/`.

## Pipeline (TaskA4)

```bash
# 1) Sanitize Dummy (strip Burst-garbage fields; seed ValueType+m_value; Delegate/Action/Invoke/Combine)
dotnet build tools/interop-runtime/SanitizeDummy -c Release
dotnet exec tools/interop-runtime/SanitizeDummy/bin/Release/net10.0/SanitizeDummy.dll \
  --input docs/evidence/dummy-x64-<stamp> \
  --output docs/evidence/dummy-x64-<stamp>-sanitized


# 2b) Enrich Dummy methods from global-metadata (Empty Dummy has type shells only)
#    Required for managed Harmony: AccessTools + NativeMethodInfoPtr via GetIl2CppMethod
dotnet build tools/interop-runtime/EnrichDummyMethods -c Release
dotnet exec tools/interop-runtime/EnrichDummyMethods/bin/Release/net10.0/EnrichDummyMethods.dll \
  --input docs/evidence/dummy-x64-<stamp>-enriched \
  --output docs/evidence/dummy-x64-<stamp>-enriched \
  --metadata "$GAME/TPM.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat" \
  --only-zero-param

# 2) Generate — use EMPTY unity-libs (input = enriched Dummy) to skip Unstrip (Unity unstrip caused Pass90 StackImbalance)
mkdir -p /tmp/empty-unity-libs
lipo "$GAME/TPM.app/Contents/Frameworks/GameAssembly.dylib" -thin x86_64 -output /tmp/GameAssembly-x64-thin.dylib
dotnet build tools/interop-runtime/GenerateIl2CppInterop -c Release
dotnet exec tools/interop-runtime/GenerateIl2CppInterop/bin/Release/net10.0/GenerateIl2CppInterop.dll \
  --input docs/evidence/dummy-x64-<stamp>-enriched \
  --output docs/evidence/il2cppinterop-out-sanitized \
  --unity-libs /tmp/empty-unity-libs \
  --game-assembly /tmp/GameAssembly-x64-thin.dylib \
  --no-xref-cache

# 3) Dedup duplicate FullNames (__c / ExecuteJobFunction) — required for Assembly.LoadFrom
cp -R docs/evidence/il2cppinterop-out-sanitized docs/evidence/il2cppinterop-out-deduped
dotnet build tools/interop-runtime/DeduplicateInteropTypes -c Release
dotnet exec tools/interop-runtime/DeduplicateInteropTypes/bin/Release/net10.0/DeduplicateInteropTypes.dll \
  --input docs/evidence/il2cppinterop-out-deduped --inplace

# 4) Deploy (keep UpdateInteropAssemblies=false)
cp docs/evidence/il2cppinterop-out-deduped/*.dll "$GAME/BepInEx/interop/"
```

## Walls cleared / notes

- Pass40 `RewriteTypeRef` NRE: Burst `$BurstDirectCall` field TypeRefs → strip in SanitizeDummy
- Pass60: Dummy primitives were ref types without `m_value` → seed ValueType + m_value
- Pass60/61: Action not MulticastDelegate / missing Delegate.Combine → seed hierarchy + Invoke/Combine/Remove
- Pass90 StackImbalance: Unity base-lib Unstrip method bodies → use empty `--unity-libs`

## loadErrs note
`DeduplicateInteropTypes` repairs Dummy soft artifacts post-generate:
1. FullName / scoped-name dedup (`_dN` / `_sN` rename)
2. `RepairInvalidObjectUsages` — BaseType, interfaces, generic constraints, field/method sigs:
   `Object<Object>` → plain `Il2CppSystem.Object`
3. `FixOrphanNestedVisibility` — root types with Nested* visibility → Public/NotPublic
   (CLR TypeLoad on NestedPublic without DeclaringType)

Cold-start (Asia/Shanghai 2026-10-20): Il2Cppmscorlib GetTypes loadErrs **430→63→1**;
UnityEngine.CoreModule **→0**; String PASS. Residual hard walls: mscorlib `Display` (1),
TPS.Game collision survivors (~21). See `docs/evidence/interop-runtime-loaderrs-converge-summary.txt`.

## Empty Dummy → managed Harmony (2026-10-20)
`AsmResolverDllOutputFormatEmpty` emits TypeDefs without MethodDefs. Il2CppInterop therefore
produced interleaved stubs with `declaredMethods≈0` / `NativeMethodInfoPtr*=0`, so Multiselect
`AccessTools.Method(…, OnBecomeActive)` SKIP'd. Fix: **EnrichDummyMethods** injects zero-param
method shells from `global-metadata.dat` (names/attrs; void sig) before Generate. Runtime
`.cctor` resolves pointers via `IL2CPP.GetIl2CppMethod` (no GameAssembly rewrite).
SanitizeDummy no longer strips methods by default (`--strip-all-methods` opt-in).

