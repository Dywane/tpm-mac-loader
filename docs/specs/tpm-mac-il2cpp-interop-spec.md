# Spec: Two Point Museum — Mac 原生 IL2CPP interop 证明（运行时 dump）

- **状态:** 已批准（2026-10-19 Asia/Shanghai）；**修订 A1–A2** 同日；**修订 A3** 同日（用户确认）：**双轨并行** — 继续 Mac reg RE + 升格 R4 C API 探针
- **日期:** 2026-10-19（Asia/Shanghai）
- **主题 slug:** `tpm-mac-il2cpp-interop`
- **代码与文档落点（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select`（`docs/specs|plans|research` + 源码）
- **调研依据（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select/docs/research/tpm-mac-il2cpp-interop-research.md`
- **门禁失败前因:** `/Users/weidu/Documents/Project/TPM-muti-select/docs/task4-interop-gate.md`
- **与多选规格关系:** 暂停执行 `tpm-mac-multiselect` 计划 Task 5–9 玩法；本规格**只**证明进程内托管调用桥。多选规格成功标准第 3 条（互操作）由本规格接管；玩法条目暂不验收。

---

## 1. 背景（为何重开）

### 1.1 已通 / 已断（证据）

| 侧 | 状态 | 证据 |
|----|------|------|
| A — Mac Loader（BepInEx + delayed Execute） | **已通** | `docs/task1-loader-smoke.md`、插件可加载 |
| B — 多选玩法 | **停** | 用户要求；Task 4 门禁失败后回退 |
| IL2CPP interop 静态生成 | **失败** | Cpp2IL `2022.1.0-pre-release.21`：x64 `codereg` 落在 Mach-O `__bss`、`metareg=0`；arm64 双 0 |

根因（证据驱动）：registration 结构在运行时写入 BSS；磁盘 `GameAssembly.dylib` 该段无初值，静态 Cpp2IL 搜不到 `MetadataRegistration`。不是「Mac 没人做 Loader」，是 **静态分析链路断了**。

### 1.2 用户选定路径（2026-10-19）

**修订 A3：双轨并行（节省时间）** — 两轨同时推进，**任一轨**先满足 §4 成功标准第 3 条（进程内读到游戏类型/字段并打日志）即可标本规格「调用桥」通过；另一轨可择机收束或继续作长期托管 DX 投资。

| 轨 | 内容 | 状态 |
|----|------|------|
| **轨 A — reg → Il2CppInterop** | 继续有界 RE / 进程内定位 Mac 非零 `codereg`+`metareg`（可继续用 A2 Win PE 对照地图）；再 Cpp2IL → Il2CppInterop → 托管探针 | 进行中；Task 2 / 2.5-A 曾阴性，允许继续收窄，**不**因并行而停 |
| **轨 B — IL2CPP C API** | 升格原 R4：P/Invoke / 原生薄封装调用 `il2cpp_*`（`class_from_name` / `runtime_invoke` 等），按名字解析类型 | **A3 升格为主验收并列路径** |

**A2 仍有效：** Win PE 仅对照地图；禁止 Win interop 直拷 Mac；PE 虚址 ≠ Mac RVA。

**非本规格：** 完整原生 trampoline 多选玩法、CrossOver、Denuvo 对抗。

---

## 2. 目标用户

| 阶段 | 用户 | 说明 |
|------|------|------|
| 本 demo | 用户本人（Mac Steam 原生 TPM） | 在本机证明调用桥；不面向工坊发布 |
| 后续 | 多选 / 其它 QoL 插件作者（含自己） | **仅在本规格通过后**再恢复玩法规格 |

---

## 3. 要证明什么（Demo 命题）

在 **原生 Mac Steam** Two Point Museum（AppID `2185060`，Unity `6000.0.63f1`，metadata v31，universal `GameAssembly`）上：

1. 不依赖磁盘静态搜到的 registration；
2. 从**已启动且 Loader 已附着**的进程取得可用的 CodeRegistration / MetadataRegistration 地址（或含 BSS 的有效内存映像）；
3. 据此生成可用的 Il2CppInterop（或经文档批准的等价托管桥）；
4. 进程内插件能解析**任意一个**游戏托管类型或字段，并写入 BepInEx 日志。

**一句话：** 先搞通「能调游戏 C# 类型」，再谈多选。

---

## 4. 成功标准（可验收）

在用户本机原生 Mac Steam 安装上，按**轨**验收（A3）：

**两轨共用硬门槛（必须）：**

- **最小进程内调用：** 已加载的探针在进程内解析到**任意一个**游戏托管类型或字段（或经 C API 得到等价 `Il2CppClass*` / 类型名），写入 `LogOutput.log`（或同等 BepInEx 日志）。不要求选择/建造相关类型。
- **可复现 / 工程落点 / 约束遵守：** 同修订前第 4–6 条（脚本或清单可复现；落点在本机仓库；不换 CrossOver、不对抗 Denuvo）。

**轨 A 额外（仅当走 Il2CppInterop 交付时）：**

1. **地址可得：** 非零 Mac `codereg` 与 `metareg` 写入 `docs/evidence/`。
2. **生成成功：** 上述地址驱动 Cpp2IL → Il2CppInterop，无致命错误。

**轨 B 额外（仅当走 C API 交付时）：**

1. **导出确认：** `GameAssembly` 上所用 `il2cpp_*` 符号经 `nm`/`dlsym` 记录。
2. **按名解析：** 至少一次 `il2cpp_class_from_name`（或文档等价）非空，并打日志。

**本规格通过定义（A3）：** 共用硬门槛满足，且**至少一轨**的「额外」条款满足。另一轨未完成不阻断「调用桥」通过，但门禁笔记须标明通过轨（A / B / 双通）。

**明确不算成功：** 仅插件加载；仅 metadata 可读；仅 Win PE Cpp2IL 成功；仅 Win interop 拷入 Mac。

---

## 5. 非目标

- Shift 多选拾取 / 旋转 / 删除及任何玩法行为对标。
- CrossOver / Wine / Proton 路径。
- 绕过或削弱 Denuvo / 其它反篡改。
- 官方装饰物 Workshop SDK 内容模。
- 以 Windows PE 生成的 interop **原样拷入 Mac** 为主交付（修订 A2：Win 仅作对照地图；同 build 下 Win/Mac `global-metadata.dat` 哈希已证**不一致**，禁止默认混用）。Win 离线成功不能单独标本规格通过。
- ~~以纯 IL2CPP C API 为禁止主交付~~ → **A3 废止本条限制**：C API 为并列主验收轨；仍禁止把「仅 Loader 启动」算通过。
- Intel Mac 优先支持（维持既有 Apple Silicon 优先；若 dump 仅在其一架构验证，须在门禁笔记写明）。
- 工坊一键安装体验、向其他玩家发布。

---

## 6. 方案（主路径）

### 6.1 机制

IL2CPP 启动后将 CodeRegistration / MetadataRegistration 写入 `__bss`。静态 `lipo` 切片上 Cpp2IL 失败（Task 4）；A1 有界磁盘 xref 亦未钉住 MetaReg，且原启发式 CodeReg 已证伪。在 Loader 已附着、il2cpp 已初始化之后：

1. **对照（R3 地图）：** 用本机已备的同 build Windows PE（见修订 A2）上 Cpp2IL 已得的非零 `codereg`/`metareg` 与搜索策略，提炼可迁移的结构签名 / 初始化写入模式（**不**把 PE 虚址当 Mac RVA）。
2. **定位（Mac）：** 两 registration 地址——进程内探针优先；允许结合对照地图的反汇编/xref；外部调试器仅在 SIP/权限允许且用户可操作时作为备选。
3. **喂入** Cpp2IL 手动地址（或 dump 含 BSS 的映像后再分析）——目标二进制必须是 **Mac** 切片。
4. **生成** Dummy → Il2CppInterop 程序集，放入 BepInEx interop 目录（或项目约定路径）。
5. **探针插件**引用 interop，读任意游戏类型/字段并打日志。

### 6.2 工具钉死（计划阶段可微调补丁号，须写入门禁笔记）

| 项 | 约束 |
|----|------|
| Loader | 沿用仓库已通的 BepInEx Mac IL2CPP 钉死版本（见既有 plan / task1 笔记） |
| Cpp2IL | 不低于已试的 `2022.1.0-pre-release.21`；若改用更新构建须记录版本与结果 |
| Il2CppInterop | 与所用 BepInEx BE 捆绑版本一致；注意公开 Windows TPM 仍有 XrefScan 类失败先例（BepInEx #1109），若撞上须记入门禁并评估是否换生成选项 / 更新构建，**不得**为此绕 Denuvo |
| 架构 | 优先验证用户实机运行的切片（Apple Silicon → arm64）；x64 对照可选 |

### 6.3 失败回退（本规格内文档化，不自动改主路径）

| 顺序 | 回退 | 触发 |
|------|------|------|
| R1 | 改进程内探针（放弃被 SIP 挡住的外部 lldb） | 外部调试不可用 |
| R2 | 记录「地址已得但 Il2CppInterop 生成失败」并开子问题（XrefScan 等） | 类似 #1109 |
| R3 | **已升格为辅助主路径（修订 A2）：** Win PE 对照地图指导 Mac 定位；Win 离线 Cpp2IL 成功**不**单独标本规格通过 | 见 A2；若对照后 Mac 仍长期无 reg → 请用户抉择 R4 |
| R4 | **A3 已升格为轨 B（并行）**，不再是失败后才启动的回退 | 与轨 A 并行；见 §10 A3 |

---

## 7. 风险与假设

| 项 | 说明 |
|----|------|
| SIP / Hardened Runtime | **未验证** Steam TPM 是否允许外部调试；规格要求优先进程内读取 |
| ASLR | 地址每次启动可能变；门禁要求可复现流程，不是单次魔法数字 |
| Il2CppInterop 第二道坑 | Windows 上曾 Cpp2IL 成功仍生成失败（#1109）；Mac 上**未验证** |
| 模块基址 | 历史上 Mac 上 `Process.Modules` 可能不见 dylib（Il2CppInterop #206）；运行时解析需留意 `dlopen`/`dladdr` 类修复是否在所用构建中（**未验证**） |
| 版本绑定 | 游戏小版本更新后 dump/生成可能作废；门禁笔记须钉 Unity / 构建号 |

---

## 8. 交付物（本规格通过时）

1. 本文件（已批准版本）位于 `docs/specs/tpm-mac-il2cpp-interop-spec.md`。
2. 门禁笔记 + `docs/evidence/` 下地址与生成/调用日志。
3. 可复现步骤（脚本或清单）与最小探针插件（能打出第 3 条日志）。
4. （可选协作镜像）`/workspace/demo-lab/specs/` 同名副本。

**不交付：** 多选玩法、实现计划正文（计划由「计划架构」在本规格批准后另写）、工坊发布包。

---

## 9. 批准后下游

1. **计划架构** 根据本规格写 `docs/plans/tpm-mac-il2cpp-interop-plan.md`（可验证小任务 + 门禁）。
2. **demo建造** 仅在计划批准后按计划实现；在此之前不恢复多选 Task 5–9。
3. 本规格通过后，再开多选规格修订或恢复原计划中的玩法任务。

---

## 10. 修订记录

### 修订 A1 — 2026-10-19（Asia/Shanghai）：授权 §6.1 反汇编深挖 MetaReg

- **用户授权：** Task 3 `MetaReg=0` 阻塞下，允许有界深挖：从 `il2cpp_init` / metadata Register / 已知 CodeReg 写入路径，用反汇编（Ghidra / Hopper / IDA，或经 [REA](https://github.com/morluto/rea) MCP 驱动上述后端）定位 `s_Il2CppMetadataRegistration`（或等价全局）的稳定 RVA，再在进程内读取验证。
- **REA 定位（证据）：** REA 是本地逆向 MCP；原生分析依赖已安装的 Hopper/Ghidra/IDA。其托管解析明确**不**解码 IL2CPP metadata；IL2CPP 路线为配对 Unity metadata + 对生成原生二进制做 Ghidra/Hopper 分析（语义恢复有限）。因此 REA **可选**作 xref/反编译工作流，**不**替代门禁成功标准，也**不**保证一键给出 MetaReg。
- **有界：** 目标仅为非零、可校验的 MetaReg（+ 既有 CodeReg）写入 `docs/evidence/`；禁止为逆向而绕 Denuvo / 换 CrossOver / 恢复多选玩法。一轮有界深挖仍失败 → 再请用户在 R3 / R4 间抉择（R4 须另开规格修订才升格主验收）。
- **状态：** 主路径仍为运行时 dump；本修订不改变成功标准 1–6 条文，仅明确允许的深挖手段与工具边界。

### 修订 A2 — 2026-10-19（Asia/Shanghai）：R3 Win PE 对照地图（用户选定）

- **触发：** A1 有界深挖阴性（MetaReg 未定位；原 Task2 CodeReg RVA `0x34D4F68` 证伪）。用户授权拉同版本 Windows depot 后选定「用 Win PE 当对照地图，继续攻 Mac」，**不**升格 R4。
- **已备工件（证据）：** `/Users/weidu/Documents/TPM-win-depot/`（仓库外）；`GameAssembly.dll`；`TPM_Data/il2cpp_data/Metadata/global-metadata.dat`；buildid **`25799969`**（与 Mac 一致）。笔记：`docs/evidence/win-depot-r3-notes.md`。
- **Win Cpp2IL（pre.21）硬事实：** 非零 `codereg`=`0x1840FFE40`，`metareg`=`0x185BC8850`；Binary init 成功。日志：`docs/evidence/win-depot/cpp2il-win-pe.log`。
- **metadata 跨平台：** 同 buildid 下 Win/Mac `global-metadata.dat` **SHA-256 不一致**（大小亦不同）→ **禁止**假设 Win Dummy/Il2CppInterop 可直接用于 Mac 进程。
- **本修订做什么：** 把 R3 从「可选对照实验」升为**允许且优先使用的辅助手段**：从 PE 侧提炼结构/搜索/初始化写入模式，指导 Mac 进程内或有界反汇编定位 **Mac** 的非零 `codereg`/`metareg`，再走原成功标准 1–6。
- **本修订不做什么：** 不改成功标准条文（仍以 Mac 进程内托管调用为硬门槛）；不把 PE 地址当 Mac 地址；不恢复多选玩法；不升格 R4；不对抗 Denuvo / 不换 CrossOver。
- **下游：** 用户确认本修订后 → **计划架构** 增补/改写 plan 中「对照地图 → Mac 定位」可验证任务；**demo建造** 待命，勿在计划未批前自行升格 R4。
- **状态：** 主验收路径名称仍为「运行时 dump / 手动 reg → Mac interop」；R3 为获准辅助。

### 修订 A3 — 2026-10-19（Asia/Shanghai）：双轨并行（reg RE + C API）

- **用户指示：** 「分开两步走，一个去继续找 reg，另一个做 IL2CPP，双管齐下」。在 Task 2 / 2.5-A 阴性后，不单选停 R1 或只升 R4，而是**并行**。
- **轨 A：** 继续 Mac 非零 `codereg`/`metareg` 定位（有界 RE / 进程内；A2 Win 对照地图仍可用）；目标仍是 Il2CppInterop 托管 DX。
- **轨 B：** 原 R4 **升格**为并列主路径 — IL2CPP C API 最小探针（按名解析类型并打日志）。
- **通过规则：** §4 改为「共用硬门槛 + 至少一轨额外条款」；门禁须标注通过轨。
- **不做：** 多选玩法；CrossOver；Denuvo 对抗；Win interop 直拷 Mac；「分表重建」若超出当前计划默许范围须再开修订（本 A3 **不**自动授权无限扩大 RE 范围，但授权与轨 B 并行的继续收窄）。
- **下游：** 计划架构改写 plan 为双轨任务；demo建造可双轨同时推进（或两人/两工作流），互不阻塞停工。

