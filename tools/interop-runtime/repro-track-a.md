# 轨 A 可复现清单（TaskA6 + TaskA7-lite Display + TaskA8 TPS.Game sealed bases）

Asia/Shanghai · Mac Steam Two Point Museum · x64 Rosetta

## 前置
- Loader `be.789+6cea286` 已装；`UpdateInteropAssemblies=false`
- 废止：`0x34D4F68` / `0x34CC0D0`；禁止 Win interop 直拷
- 工程：`plugins/interop-probe/` + `tools/interop-runtime/`；不改 `loader/` / `multiselect/` / C API Host

## 步骤

### 1. 冷启动取址 + 在线 Dummy（TaskA2/A3）
```bash
export TPM_INTEROP_EVIDENCE_DIR="$REPO/docs/evidence"
export TPM_GAME_ROOT="$GAME"
cd "$GAME" && arch -x86_64 ./run_bepinex.sh
# 日志：Non-zero codereg+metareg；dummySource=AsmResolverDllOutputFormatEmpty；dllCount=153
```

### 2. Sanitize Dummy（TaskA4 预清洗）
```bash
dotnet build tools/interop-runtime/SanitizeDummy -c Release
dotnet exec tools/interop-runtime/SanitizeDummy/bin/Release/net10.0/SanitizeDummy.dll \
  --input docs/evidence/dummy-x64-<stamp> \
  --output docs/evidence/dummy-x64-<stamp>-sanitized
# 去掉 Burst $BurstDirectCall 字段；种子 System.* ValueType+m_value；Delegate/Action Invoke+Combine
```

### 2b. Enrich Dummy methods（Empty → MethodDefs）
```bash
dotnet build tools/interop-runtime/EnrichDummyMethods -c Release
dotnet exec tools/interop-runtime/EnrichDummyMethods/bin/Release/net10.0/EnrichDummyMethods.dll \
  --input docs/evidence/dummy-x64-<stamp>-sanitized \
  --output docs/evidence/dummy-x64-<stamp>-enriched \
  --metadata "$GAME/TPM.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
# CursorModePlaceItem.OnBecomeActive / RestartItemPlaceState 必须出现在 Dummy MethodDefs
```

### 3. Il2CppInterop 生成（空 unity-libs）
```bash
mkdir -p /tmp/empty-unity-libs
lipo "$GAME/TPM.app/Contents/Frameworks/GameAssembly.dylib" -thin x86_64 -output /tmp/GameAssembly-x64-thin.dylib
dotnet build tools/interop-runtime/GenerateIl2CppInterop -c Release
dotnet exec tools/interop-runtime/GenerateIl2CppInterop/bin/Release/net10.0/GenerateIl2CppInterop.dll \
  --input docs/evidence/dummy-x64-<stamp>-enriched \
  --output docs/evidence/il2cppinterop-out-sanitized \
  --unity-libs /tmp/empty-unity-libs \
  --game-assembly /tmp/GameAssembly-x64-thin.dylib \
  --no-xref-cache
# 期望：outDlls=153 exit=0
```

### 4. 去重嵌套/同名类型（TaskA5 LoadFrom）
```bash
cp -R docs/evidence/il2cppinterop-out-sanitized docs/evidence/il2cppinterop-out-deduped
dotnet build tools/interop-runtime/DeduplicateInteropTypes -c Release
dotnet exec tools/interop-runtime/DeduplicateInteropTypes/bin/Release/net10.0/DeduplicateInteropTypes.dll \
  --input docs/evidence/il2cppinterop-out-deduped --inplace
# TaskA7-lite: RepairBogusHandleBases (Display→Object)
# TaskA8: RepairSealedOrPrimitiveBases (TPS.Game parent_sealed→Il2CppObjectBase); expect sealedBasesFixed≥0 idempotent
cp docs/evidence/il2cppinterop-out-deduped/*.dll "$GAME/BepInEx/interop/"
```

### 5. 托管探针（TaskA5）
冷启动 `TpmMacInteropProbe` ≥0.5.34 → 日志 `[A5] managed probe OK … type=Il2CppSystem.String`
证据：`docs/evidence/interop-runtime-managed-call.log`

## 工程边界
| 可动 | 禁动 |
|------|------|
| `plugins/interop-probe/`、`tools/interop-runtime/`、`docs/evidence/interop-*` | 玩法 / C API / Host / multiselect / Loader 钉死包 |

## 成功映射（轨 A）
- [x] 非零 Mac codereg+metareg（合成 live）
- [x] Cpp2IL Dummy TypeDefs（A3b）
- [x] Il2CppInterop outDlls=153（A4）
- [x] LoadFrom + 解析 Il2CppSystem.String（A5）

## TaskA8 离线（TPS.Game ×21）
- [x] 分类 parent_sealed=21 → `docs/evidence/interop-runtime-loaderrs-tps-game-classify.txt`
- [x] RepairSealedOrPrimitiveBases → offline TPS.Game loadErrs 21→0
- [ ] 冷启动确认（待用户再开 InteropProbe；当前 `.off`）
