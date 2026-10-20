# tools/interop-runtime (轨 A)

Offline helpers: Dummy sanitize → Il2CppInterop generate → deploy `BepInEx/interop/`.

## Pipeline (TaskA4)

```bash
# 1) Sanitize Dummy (strip Burst-garbage fields; seed ValueType+m_value; Delegate/Action/Invoke/Combine)
dotnet build tools/interop-runtime/SanitizeDummy -c Release
dotnet exec tools/interop-runtime/SanitizeDummy/bin/Release/net10.0/SanitizeDummy.dll \
  --input docs/evidence/dummy-x64-<stamp> \
  --output docs/evidence/dummy-x64-<stamp>-sanitized

# 2) Generate — use EMPTY unity-libs to skip Unstrip (Unity unstrip caused Pass90 StackImbalance)
mkdir -p /tmp/empty-unity-libs
lipo "$GAME/TPM.app/Contents/Frameworks/GameAssembly.dylib" -thin x86_64 -output /tmp/GameAssembly-x64-thin.dylib
dotnet build tools/interop-runtime/GenerateIl2CppInterop -c Release
dotnet exec tools/interop-runtime/GenerateIl2CppInterop/bin/Release/net10.0/GenerateIl2CppInterop.dll \
  --input docs/evidence/dummy-x64-<stamp>-sanitized \
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
