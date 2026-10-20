# Mac IL2CPP interop — 运行时门禁笔记（tpm-mac-il2cpp-interop）

日期起步：2026-10-19（Asia/Shanghai）  
计划：`docs/plans/tpm-mac-il2cpp-interop-plan.md`  
规格：`docs/specs/tpm-mac-il2cpp-interop-spec.md`（**A3 双轨**；A1+A2 史实仍有效）

---

## ⚠ 废止声明（A1 · 显著 · 全门禁适用）

| 项 | 状态 | 动作 |
|----|------|------|
| **`0x34D4F68`** | **假阳性 CodeReg**（A1 证伪） | **禁止**再作主候选 / 喂 Cpp2IL / 写入「标准 1 通过」；仅可作反例 |
| **`0x34CC0D0`** | 磁盘静态启发式（task4） | **不可采信** |
| A1 MetaReg 有界深挖 | **阴性** | 证据：`docs/evidence/interop-runtime-registration-x64-task3-a1-metareg-negative.txt` |
| 后续定位 | 必经 **Task 1 地图启发**（或新有界手段） | Mac **双侧非零** 才可标标准 1 |

旧 Task 2 段落中凡将 `0x34D4F68` 写成「稳定候选」之处，一律以本表为准作废；见下方 Task 2 废止横幅。

---

## Task 0 — 基线锁定、停工确认、废止与 A2 指针

### 基线表

| 字段 | 值 |
|------|-----|
| Loader 钉死包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286`（`loader/VERSION.txt`；commit `6cea286`） |
| Doorstop | 4.5.0（同 VERSION.txt） |
| Unity | `6000.0.63f1`（build `9438f9b77a46`；Info.plist / LogOutput） |
| **Steam buildid** | **`25799969`**（与 Win depot 一致；R3 钉死） |
| 游戏版本串 | `12.0.244617`（CFBundleShortVersionString） |
| AppID / 路径 | Steam `2185060`；`~/Library/Application Support/Steam/steamapps/common/Two Point Museum` |
| GameAssembly | `TPM.app/Contents/Frameworks/GameAssembly.dylib`（universal **x86_64 + arm64**）；根目录软链同目标 |
| global-metadata | `TPM.app/Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat` |
| metadata magic / version | `0xFAB11BAF` / **31** |
| **Mac metadata SHA-256** | **`071ad8ace29c6ef49f5f97b8585da134c8219f8d7d44efba4dae12fb00509036`** |
| 主机 | Apple Silicon（`uname -m` = arm64）；Rosetta 可用 |
| **当前启动架构（主 dump）** | **x64（Rosetta）**：`install-loader.sh` 将 `run_bepinex.sh` 钉为 `arch -x86_64` + Doorstop；LogOutput：`Process bitness: 64-bit (x64)` |
| arm64 对照 | 可选；本计划主路径不切换 Loader 到 arm64 |
| `UpdateInteropAssemblies` | **false**（游戏 `BepInEx/config/BepInEx.cfg`） |
| `PreloadIL2CPPInteropAssemblies` | **false** |
| Mac 插件加载补丁 | delayed `Chainloader.Execute`（`loader/tools/mac-delayed-execute`）；场景 invoke detour 在 Rosetta 下不可靠 |

### 静态失败前提（不重跑无手动地址静态搜）

- `docs/task4-interop-gate.md`
- `docs/evidence/task4-cpp2il-pre21-x64.log`（codereg 落 `__bss`，`metareg=0`）
- `docs/evidence/task4-cpp2il-pre21-arm64.log`（双 0）

**声明：** 禁止以「再跑一遍**无手动地址**的静态搜」或「重采已证伪 `0x34D4F68`」冒充主路径进度。主路径 = 地图启发 → Mac 进程内非零双侧 reg → 手动地址 Cpp2IL（Mac 切片）。

### 停工确认（多选）

- **`tpm-mac-multiselect` Task 5–9 保持停**：不实现拾取 / 旋转 / 删除。
- 本计划 slug 独立：`tpm-mac-il2cpp-interop`；代码单元 = `plugins/interop-probe/`。
- `plugins/multiselect/` 可保留脚手架，**不在本计划验收**。

### A2 指针（对照地图，非直拷）

| 项 | 值 |
|----|-----|
| Win depot（仓库外只读） | `/Users/weidu/Documents/TPM-win-depot/` |
| R3 笔记 | `docs/evidence/win-depot-r3-notes.md` |
| 地图摘要 | `docs/evidence/interop-win-map-summary.md` |
| 可迁移 hints | `docs/evidence/interop-win-map-portable-hints.md` |
| 性质 | **对照地图** → 指导 Mac 定位；**禁止** Win Dummy/interop 直拷 Mac；**禁止** PE VA 当 Mac RVA |

### 约束声明

- 唯一开发落点：本机 `/Users/weidu/Documents/Project/TPM-muti-select`
- **不** CrossOver / Wine / Proton；**不**对抗 Denuvo
- **A3：** R4（IL2CPP C API）= **轨 B**，已升格与轨 A **并行**（由 `demo建造-CAPI` 交付；本门禁轨 A 段落不实现 C API）
- 轨 A 主路径：Win 对照地图启发 → 运行时非零 Mac `codereg`/`metareg` → Cpp2IL（≥ pre.21 手动地址，**Mac** 切片）→ Il2CppInterop → 进程内最小托管调用日志

**Task 0 完成判据：** 基线表（含 buildid + Mac meta SHA）+ 静态失败链接 + 多选停工 + **废止 `0x34D4F68`** + A2 指针 — 已齐。

---

## Task 1 — Win PE 对照地图（A2 · 完成）

日期：2026-10-19（Asia/Shanghai）

| 交付 | 路径 |
|------|------|
| 硬事实 + 三禁令 | `docs/evidence/interop-win-map-summary.md` |
| PortableRegHints | `docs/evidence/interop-win-map-portable-hints.md` |
| 源日志 | `docs/evidence/win-depot/cpp2il-win-pe.log` |

### 硬事实（摘录）

| 字段 | 值 |
|------|-----|
| buildid | `25799969` |
| Win codereg（**PE VA only**） | `0x1840FFE40` |
| Win metareg（**PE VA only**） | `0x185BC8850` |
| Win metadata SHA-256 | `de8c2aa40aa36188a2fe8e72bba84cff4a22cdc1edb071005ef19313491243d8` |
| Mac metadata SHA-256 | `071ad8ace29c6ef49f5f97b8585da134c8219f8d7d44efba4dae12fb00509036` |
| 哈希关系 | **不一致** → 禁止 Win interop 直拷 Mac |
| Cpp2IL | pre.21；Win Binary init 成功 |

### 三禁令

1. **禁止** PE VA → Mac RVA 换算当真相  
2. **禁止** Win Dummy / Il2CppInterop 直拷 Mac  
3. **禁止** 因 Win Binary init 勾选规格标准 2/3  

### 门禁勾选

- [x] **地图已就绪 → TaskA2（轨 A 定位）可用**
- [x] metadata 哈希已校验 **不一致**（Win ≠ Mac）
- [x] 全文不以 Win PE VA 冒充 Mac 地址

### 工程空架（计划修订前曾标 Task 1；仍可用）

- 源码：`plugins/interop-probe/`（`TpmMacInteropProbe.csproj`，net6.0，AllowUnsafeBlocks）
- 契约：`IRegistrationLocator` / `RegistrationAddresses` / `RegistrationEvidenceWriter` / `MinimalManagedCallProbe`
- 基址：`DlopenRegistrationLocator` 用 `dlopen(RTLD_NOLOAD)` + `dlsym(il2cpp_init)` + `dladdr`
- Evidence：`interop-runtime-registration-<arch>-<runId>.txt`
- **定位结论须按 A1/A2 作废重验**（`0x34D4F68` 不得沿用）；Locator 须接受 `PortableRegHints` 并拒绝废止 RVA

---

## Task 2 — 历史 registration 尝试（⚠ 主路径结论已废止）

> ### ⚠ 废止横幅（A1 / A2）
>
> 下文保留作**史实与反例**。  
> **`0x34D4F68` 不再是「稳定 CodeReg 候选」**，不得喂 Cpp2IL，不得标标准 1 通过。  
> MetaReg 假命中早已撤销；A1 有界反汇编进一步证伪该 BSS 槽。  
> **现行 Task 2（计划 A2）：** 已执行地图启发定位 → **阴性 / BLOCKED**（双侧仍 0）。证据：`docs/evidence/interop-runtime-registration-x64-task2-a2-negative.txt`。

日期：2026-10-19（Asia/Shanghai）

### 历史结果（已作废 — 勿采信为主路径）

| 字段 | 历史记载 | 现行状态 |
|------|----------|----------|
| CodeReg RVA `0x34D4F68` | 曾标「BSS；ASLR 稳定」；首字段 `0x99` | **废止假阳性**（A1：`0x2E9C01` 写入派生 dword） |
| MetaReg | `44351`/`44453` 堆址 | **假命中已撤销**（`…7ED9` cookie；`typeSizes≠0x78FA`） |

证据（反例保留）：

- `docs/evidence/interop-runtime-registration-x64-20261019044351-a1.txt`
- `docs/evidence/interop-runtime-registration-x64-20261019044453-a1.txt`

### 历史定位策略（CodeReg — 已废止为主路径）

1. 基址：`dlopen(GameAssembly, RTLD_NOLOAD)` + `dladdr(il2cpp_init)` — **基址手法仍可用**。
2. ~~稳定候选 RVA `0x34D4F68`；校验 `0x99`~~ → **禁止 ACCEPT**。
3. 延迟：Chainloader 后重试 — 仍适用于新 Locator。

### ASLR 自检（历史）

- 相对 RVA `0x34D4F68` 曾不变 — **不证明其为真 CodeReg**。
- 绝对 VA 随基址变 — ASLR 流程要求仍有效，须对新候选重做。

### 现行定位策略优先级（A2）

1. **进程内 + PortableRegHints**（主路径）：见 `interop-win-map-portable-hints.md`
2. 有界 RE（规格 A1 授权边界）：目标仅为 Mac 非零双侧 reg
3. 外部 lldb：SIP 挡 → **R1**

---



### Task 2 — A2 地图启发定位（2026-10-19 Asia/Shanghai）— **阴性 / BLOCKED**

| 项 | 结果 |
|----|------|
| 探针 | `TpmMacInteropProbe` **0.4.0**；`PortableRegHints` + `DeprecatedCandidates` |
| Mac type-count | **0x78FA**（30970）已复核 |
| CodeReg pcm-128 | 指向 `0x2D2FA90` 的数据指针 **hits=0**（两冷启动） |
| CodeReg mscorlib 链 | 表可解析；**内联**（无 pCodegenModules 存储） |
| 废止 `0x34D4F68` | **HARD-REJECT**；结果 codereg=**0**（不再假 ACCEPT） |
| MetaReg exact 0x78FA | 无 ACCEPT（cookie / 布局不匹配） |
| ASLR | base `0x12C2BF000` → `0x130BBF000`；modules **RVA 0x2D2FA90** 稳定 |
| 标准 1 | **未勾选** |

代表证据：

- `docs/evidence/interop-runtime-registration-x64-task2-a2-negative.txt`
- `docs/evidence/interop-runtime-registration-x64-20261019071927-a1.txt`（run1）
- `docs/evidence/interop-runtime-registration-x64-20261019072328-a1.txt`（run2）

**下一跳（史实）：** 当时请用户抉择。**A3 后：** 见 §A3 / §TaskA2；R4 已升格为轨 B 并行。多选 Task 5–9 仍停。

## Task 3 — MetaReg / Cpp2IL（史实 · MetaReg 阴性；等 Task 2 新地址）

日期：2026-10-19（Asia/Shanghai）

### codeGenModules dump（结论：**非 per-assembly**）

静态 `g_CodeGenModules` RVA **`0x2D2FA90`**（`__DATA.__const`，153 项；`mscorlib` 在 index **152**）。

运行时读 mscorlib `Il2CppCodeGenModule`（Il2CppDumper 布局，含 adjustorThunk）：

| field | 含义 | 运行时值 |
|------|------|----------|
| 15 | `metadataRegistration` | **0** |
| 16 | `codeRegistration` | **0** |
| 14 | `staticConstructorTypeIndices` 邻域 / 对照 | **0**（per-assembly 槽均为 0） |

→ Unity 6000.0.63f1 / metadata 31 **不是** per-assembly registration；不能从 CodeGenModule 尾部取 MetaReg。

> 关于历史 BSS「CodeReg」`0x34D4F68`：A1 已证伪；`+8` 堆指针亦非静态 modules 表。详见废止声明。Win 对照 module count 为 **152**（见地图）；Mac 侧勿再以假 `0x99` 槽 ACCEPT。

### MetaReg 搜索（阴性）

| 手段 | 结果 |
|------|------|
| BSS struct：`Low32(+0x60)==0x78FA` | `exact78FA=0` / `structTries=0` |
| BSS→堆指针 chase + `TryValidateMetaRegQuiet` | `ptrTries≈6.3k`，0 ACCEPT；拒 `…7ED9` cookie |
| 映像/堆 `Low32==0x78FA` 页扫 | 有命中；校验失败（cookie / 偶奇字段不像 MetaReg） |
| CodeReg±0x100 邻域 | 无 ACCEPT |
| 静态映像 MetaReg shape（`fieldOff==typeSizes==0x78FA`） | **0**（与 task4「磁盘 metareg=0」一致） |

代表证据：`docs/evidence/interop-runtime-registration-x64-task3-metareg-negative.txt`（源自 `20261019061227-a1`）。

### Cpp2IL

**未跑通**（缺真实非零 MetaReg；**禁止**拿废止 `0x34D4F68` 或 Win PE VA 喂 Dummy）。

### 探针改动（史实）

- `plugins/interop-probe/Registration/DlopenRegistrationLocator.cs`：静态 modules dump、Low32+cookie 过滤、邻域扫描等 — Task 2 须改为消费 hints 并拒绝废止 RVA。

### 修订 A1 有界反汇编（2026-10-19 Asia/Shanghai）— **阴性**

工具：Capstone 5 + `tools/re/a1_metareg_xref.py`（无 Ghidra/Hopper/IDA）。

| 结论 | 细节 |
|------|------|
| Task2 CodeReg `0x34D4F68` | **假阳性**：`0x2E9C01` 写入的是 `f(obj+0xAC)` 派生 dword（恰为 0x99），非 `Il2CppCodeRegistration` |
| CodeGenModules | **内联**：`0x2E9E93 lea → 0x2D2FA90`；无 `&MODULES`→BSS CodeReg 指针写 |
| MetaReg 全局 | **未找到** `s_Il2CppMetadataRegistration`（无 0x78FA 静态/imm、无 Register 双 store、无符号/字符串） |
| Cpp2IL `0x34CC0D0` | 映像无 xref；不采信 |

证据：`docs/evidence/interop-runtime-registration-x64-task3-a1-metareg-negative.txt`

**Cpp2IL / Dummy：** 仍未跑通。

**下一跳（史实）：** Task 2 已执行并阴性；见下 §2.5-A。

---


### 修订 A2 Task 2.5-A 加深 RE（2026-10-19 Asia/Shanghai）— **阴性 / BLOCKED**

用户定案 **A**：继续收窄有界 RE（追 `il2cpp_init` 运行时写入）；**不**升格 R4。

相对 A1 差额：BFS 深 **6**（A1≤3）、函数窗 **0x1800**（A1 0x400）、全文 rdi+rsi Register 簇扫描、导出 API 反查、Win PE LE64 type-count 对照。

| 结论 | 细节 |
|------|------|
| Register(rdi,rsi) 双写全局 | **0** 簇（全文） |
| CodeGenModules | 仍仅 `0x2E9E93` LEA 内联；无 pCodegenModules 槽 |
| Mac 磁盘 LE64 `0x78FA` | **0**（Win PE 有 LE64 `0x7907`×2） |
| 运行时访问 | `image_get_class` / `class_for_each` 走 `0x34D4F58`+`0x34D4F60` 分表，非 MetaReg* |
| 合成 CodeReg | modules RVA `0x2D2FA90` 已知，但 **无**可校验 MetaReg → 不喂 Cpp2IL、不勾标准 1 |

证据：`docs/evidence/interop-runtime-registration-x64-task2-re-a25-negative.txt`  
工具：`tools/re/a25_il2cpp_init_deep_re.py`  
摘要：`docs/evidence/interop-runtime-registration-x64-task2-re-a25-summary.json`

**Cpp2IL / Dummy：** 仍未跑通。标准 1 **未达成**。

**下一跳（史实）：** 当时停工交接抉择。**A3 后：** R4=轨 B 已并行；轨 A=TaskA2 继续收窄（见下 §TaskA2）。多选 Task 5–9 仍停。



---

> **注明（A3）：** 上文 A2 / Task 2.5-A 史实中「**不**升格 R4」「**不**升格 R4（C API）为主验收」已被规格修订 **A3 双轨**废止。轨 B（IL2CPP C API）现为合法通过轨；下列为轨 B 门禁通过记录。轨 A 史实段落保留不删。

## 轨 B — IL2CPP C API（A3）— **通过**（2026-10-19 Asia/Shanghai）

### A3 通过框

- 共用硬门槛：☑（进程内 `class_from_name` → System.String）
- 轨 A 额外：☐（未完；不阻断）
- 轨 B 额外：☑ 导出确认（nm+dlsym）+ ☑ 按名非空
- **通过轨：B**

### 证据指针

| 文件 | 用途 |
|------|------|
| `docs/evidence/capi-exports-nm-x64.txt` | B1 磁盘 nm：所用 `il2cpp_*` 导出 PRESENT |
| `docs/evidence/capi-exports-dlsym-x64.md` | B1/B2 进程内 dlsym 8/8 非零；`il2cpp_class_from_name -> 0x130CDABD1`；wrappers ready |
| `docs/evidence/capi-class-from-name-x64.log` | B3 冷启动 LogOutput：`[CAPI] class_from_name ok ns=System name=String ptr=0x10D9E5390 got=System.String` |
| `docs/evidence/capi-probe-status.md` | B1–B4 状态总表 |
| `docs/evidence/capi-probe-repro.md` | 逐步复现清单 |

### 复现摘要

Loader 已通（`UpdateInteropAssemblies=false`；Rosetta x64）→ 部署 `BepInEx/plugins/TpmMacCapiProbe.dll`（`plugins/capi-probe/`，GUID `com.tpm.mac.capi-probe` v0.1.0）→ 冷启动 TPM → `BepInEx/LogOutput.log` 出现 `[CAPI] native wrappers ready` 与 `[CAPI] class_from_name ok`。

### 工程边界

- 落点：`plugins/capi-probe/`（独立；与轨 A `interop-probe` 分离）
- **未**改 Loader；**未**恢复多选玩法；**未** Win interop 直拷 Mac；**未**找 CodeReg/MetaReg

### 注明

轨 A 未完**不阻断**本规格调用桥通过（规格 A3）。基线：Unity `6000.0.63f1`；Steam buildid `25799969`；进程 x64 Rosetta。

### 关键验证数字（勿改）

| 项 | 值 |
|----|-----|
| `class_from_name` | `ns=System name=String ptr=0x10D9E5390 got=System.String` |
| dlsym | 8/8 非零；`il2cpp_class_from_name -> 0x130CDABD1` |
| wrappers | `[CAPI] native wrappers ready` |


---

## A3 双轨（2026-10-19 Asia/Shanghai）

| 轨 | 职责 | 执行方 |
|----|------|--------|
| **轨 A** | Mac 非零 `codereg`+`metareg` → Cpp2IL → Il2CppInterop | demo建造 / TaskA2 |
| **轨 B** | IL2CPP C API（原 R4 **已升格并行**） | demo建造-CAPI |

**R4 状态：** 不再是「失败后才启动的回退」；**= 轨 B，已并行**。删除「不得升格 R4」的过时约束（见 Task 0 约束声明已改）。

### A3 通过框模板（规格 §4）

- [ ] **共用硬门槛：** 进程内解析到任意游戏托管类型/字段（或 C API 等价）并打日志
- [x] **轨 A 额外（部分）：** 合成非零 Mac codereg+metareg + **Cpp2IL Binary Init OK**（0.5.2）；Dummy/Il2CppInterop 仍因 stub MetaReg 失败 — 见 §TaskA2-synth
- [ ] **轨 B 额外：** 所用 `il2cpp_*` 导出确认 + 至少一次按名解析非空并打日志
- **通过轨：** A / B / 双通 / **未通过** ← 门禁结案时圈一

任一轨先满足「共用 + 该轨额外」即可标本规格调用桥通过；另一轨可续作长期投资。

---

## TaskA2-synth — 轨 A 合成 CodeReg+MetaReg（A3）— **Binary Init OK / Dummy CONCERNS**

日期：2026-10-19（Asia/Shanghai）  
probe：**0.5.2**；证据：`docs/evidence/interop-runtime-registration-x64-task2-a3-synth-summary.txt`  
raw：`…20261019075615-a1.txt`；Cpp2IL：`…-task2-a3-synth-cpp2il-20261019075615.log`

| 项 | 结果 |
|----|------|
| CodeReg | 堆合成；pCodegen→**base+0x2D2FA90**；count=**153**；Accept |
| MetaReg | 堆合成；typeCount=**0x78FA**；stub 数组；Accept |
| 废止 F68 | 未 ACCEPT |
| LibCpp2IL Init | **OK**（Mac live + Mac metadata；NumTypes=30970） |
| Dummy / AnalysisContext | **FAIL**（`GetType` IndexOutOfRange — stub 类型表） |
| ASLR | 绝对合成 VA 可变；逻辑相对 modules RVA 可复现 |

- [x] TaskA2.3 / A2.4：合成双侧非零 + **Cpp2IL Binary init**（手动地址）
- [ ] TaskA3：Dummy / Il2CppInterop（被 stub MetaReg 阻断）

**status：** DONE_WITH_CONCERNS。父代理可续派「真实 runtime 数组替换 stub」；**勿**停工请用户抉择。轨 B 互不影响。

---

## TaskA2 — 轨 A 继续收窄（A3 · 分表运行时刻画）— **史实阴性**（其后见 §TaskA2-synth）

日期：2026-10-19（Asia/Shanghai）  
探针：`TpmMacInteropProbe` **0.5.1**（Track A only）

| 项 | 结果 |
|----|------|
| 角度 | **运行时** dump BSS `0x34D4F58`/`0x34D4F60` + metadata 头 + stride `0x58` 表（相对 2.5-A 静态 BFS **新**） |
| F58/F60 | 两槽同值 = **global-metadata.dat 映射基址**；`+0xA0` dword = typeDefinitions 偏移 **0x11D0A3C**（与磁盘 metadata 一致；count/`0x58`=`0x78FA`） |
| F68 | 仍 `0x99`；**未 ACCEPT** |
| MetaReg | 分表 chase / exact78FA → **无 Accept** |
| CodeReg | pcm-128 hits=0；合成 CodeReg **未**启用（缺可校验 MetaReg） |
| ASLR | base `0x130A3F000`→`0x130C3F000`；槽 RVA 稳定；堆 metadata 基址随 ASLR |
| 标准 1（轨 A） | **未勾选** |

证据：

- `docs/evidence/interop-runtime-registration-x64-task2-a3-split-negative.txt`
- `docs/evidence/interop-runtime-registration-x64-20261019074505-a1.txt`（run1）
- `docs/evidence/interop-runtime-registration-x64-20261019074701-a1.txt`（run2）

**下一跳：** 父代理可续派 TaskA2 收窄（不请用户抉择停工）。轨 B 并行互不阻塞。建议若要把分表拼成 LibCpp2IL MetaReg，另开「分表重建」规格修订。


## TaskA4 / TaskA5（2026-10-19 Asia/Shanghai）

- [x] TaskA4：SanitizeDummy → GenerateIl2CppInterop（空 unity-libs）→ outDlls=153；已装 `BepInEx/interop`
- [x] TaskA5：DeduplicateInteropTypes 去重后 `Assembly.LoadFrom(Il2Cppmscorlib)` OK；探针 `Il2CppSystem.String` fields/methods 记入 `docs/evidence/interop-runtime-managed-call.log`
- [x] TaskA6：`tools/interop-runtime/repro-track-a.md`
- [x] TaskA7-lite：`RepairBogusHandleBases` — `TypeIdentifiers+Display` BaseType
      `RuntimePropertyHandle`(sealed) → `Il2CppSystem.Object`；mscorlib GetTypes loadErrs 1→0
      离线 + 冷启动双证：Asia/Shanghai 2026-10-20 15:29:05 A5 BASELINE/AFTER loadErrs=0、无 Display、String OK
      证据 `docs/evidence/interop-runtime-loaderrs-display-fix.txt`、
      `interop-runtime-loaderrs-a5-log-excerpt-display-cold.txt`
- [x] TaskA8：`RepairSealedOrPrimitiveBases` — TPS.Game parent_sealed ×21 →
      Il2CppObjectBase；ALC GetTypes loadErrs **21→0**（typesOk 14847→14868）
      同刀顺带修同类 sealed/primitive bases 共 sealedBasesFixed=143（5 dll）
      离线 + 冷启动双证：Asia/Shanghai 2026-10-20 15:43:52 A5 EXTRA TPS.Game loadErrs=0、
      mscorlib still 0、无 Display、String OK
      证据 `docs/evidence/interop-runtime-loaderrs-tps-game-classify.txt`、
      `interop-runtime-loaderrs-tps-game-fix.txt`、
      `interop-runtime-loaderrs-a5-log-excerpt-tps-game-cold.txt`
      分支/worktree：`track-a/tps-game-loaderrs`（未 merge 主树 / CAPI）
      探针：冷启动临时启用后已改回 `.dll.off`
- 通过轨（轨 A 额外）：生成半段 + 托管探针半段已满足；共用硬门槛经 Il2CppInterop 路径打日志
- **下一跳：** 轨 A GetTypes 硬墙（mscorlib Display + TPS.Game×21）已清；merge 主线待用户点头

## TaskA9 — InteropRuntimeKernel 接通（本 worktree 默认 Interop）

日期：2026-10-20（Asia/Shanghai）  
分支/worktree：`track-a/tps-game-loaderrs`（**未** merge 主树）  
Host：**0.3.0** Interop-only

| 项 | 结果 |
|----|------|
| DefaultKernelKind | **Interop**（CApi 从本分支 csproj 排除；`plugins/capi-probe` 移至 `plugins/_excluded_track_a/`） |
| Ready 判据 | `BepInEx/interop` + LoadFrom `Il2Cppmscorlib` + `Il2CppSystem.String` fields/methods 可读 |
| 冷启动 | **PASS** — `[HOST] Runtime Kind=Interop Ready=true`；String smoke OK；`TPS.Game.BuildMode` resolve OK |
| TryInvoke/Field/Find | **NotSupported**（A9 minimal；诚实 false + 诊断） |
| InteropProbe | 保持 `.dll.off`（用 Host 自身日志证明） |
| 证据 | `docs/evidence/interop-runtime-kernel-a9-cold.txt` |

- [x] TaskA9.1：本分支只走 Interop（Load 零 CAPI）
- [x] TaskA9.2：InteropRuntimeKernel 真 Ready + TryResolveType（String + TPS.Game）
- [x] TryInvoke / Field deferred from A9 → **TaskA10** (Find still NotSupported)

## TaskA10 — InteropRuntimeKernel TryInvoke / Field（本 worktree）

日期：2026-10-20（Asia/Shanghai）  
分支/worktree：`track-a/tps-game-loaderrs`（**未** merge 主树）  
Host：**0.3.1** Interop-only

| 项 | 结果 |
|----|------|
| TryInvoke | **PASS** — `op_Implicit("a10")` → RuntimeObjectHandle |
| TryGetField | **PASS** — `isWrapped` → true value=False |
| TrySetField | **PASS** — idempotent rewrite `isWrapped=False` |
| TryGetTypeName | **PASS** — `Il2CppSystem.String` |
| TryFindObject | **NotSupported**（诚实 false） |
| InteropProbe | 保持 `.dll.off`（Host 自身 smoke 日志证明） |
| 证据 | `docs/evidence/interop-runtime-kernel-a10-cold.txt` / `interop-runtime-kernel-a10-status.txt` |

- [x] TaskA10：最小 Invoke + GetField（+ SetField 幂等写）冷启动取证
- [ ] TryFindObject / 玩法钩子 / merge 主树（待用户）

**说明：** 当前 Empty Dummy 的 `Il2CppSystem.String` DeclaredOnly 仅有 `op_Implicit`；无 `get_Empty`。Field smoke 用 `Il2CppObjectBase.isWrapped`。

