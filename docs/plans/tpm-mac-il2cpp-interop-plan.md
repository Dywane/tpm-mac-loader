# Plan: Two Point Museum — Mac 原生 IL2CPP interop 证明（tpm-mac-il2cpp-interop）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **依据规格已批准：** 2026-10-19（Asia/Shanghai）；**修订 A1–A3** 同日（用户已确认 A3 双轨并行）  
> **主题 slug：** `tpm-mac-il2cpp-interop`（**独立于** `tpm-mac-multiselect`）  
> **文档权威落点（本机仓库）：** `/Users/weidu/Documents/Project/TPM-muti-select/docs/`  
> **规格：** `docs/specs/tpm-mac-il2cpp-interop-spec.md`（**A1+A2+A3**）  
> **计划：** `docs/plans/tpm-mac-il2cpp-interop-plan.md`（本文件权威副本）  
> **调研：** `docs/research/tpm-mac-il2cpp-interop-research.md`  
> **前因门禁：** `docs/task4-interop-gate.md` + `docs/evidence/task4-cpp2il-pre21-*.log`  
> **运行时门禁笔记：** `docs/interop-runtime-gate.md`  
> **R3 证据：** `docs/evidence/win-depot-r3-notes.md`；Win depot（仓库外）：`/Users/weidu/Documents/TPM-win-depot/`  
> **可选镜像：** `/workspace/demo-lab/`（非权威；有改动时以本机 `docs/` 为准并回写）  
> **若规格变更：** 必须同步更新本计划，并在本段追加变更记录（日期 + 条目）。  
>
> **执行分工（A3 钉死）**  
> | 轨 | 执行方 | 工程落点 | 主交付 |  
> |----|--------|----------|--------|  
> | **轨 A** | **demo建造** | `plugins/interop-probe/` | Mac reg → Cpp2IL → Il2CppInterop → 托管探针 |  
> | **轨 B** | **demo建造-CAPI** | `plugins/capi-probe/`（新建） | IL2CPP C API：导出确认 → P/Invoke → `class_from_name` 非空打日志 |  
> | 共用基线 | 任一轨开工前自检；可由 demo建造 维护门禁叙述 | `docs/interop-runtime-gate.md` Task 0 | 废止地址、Loader、停多选 |  
>
> **变更记录**  
> - 2026-10-19：初版。规格已批准；主路径 = 运行时 dump 非零 `codereg`/`metareg` → Cpp2IL → Il2CppInterop → 进程内最小托管调用。多选 Task 5–9 保持停。  
> - 2026-10-19 **A1**：规格授权有界反汇编深挖 MetaReg。执行结果：**阴性**（MetaReg 未定位）；原启发式 CodeReg RVA **`0x34D4F68` 证伪**（假阳性）。证据：`docs/interop-runtime-gate.md` §A1；`docs/evidence/interop-runtime-registration-x64-task3-a1-metareg-negative.txt`。  
> - 2026-10-19 **A2**：R3 升格为辅助主路径 — Win PE 对照地图。Win Cpp2IL 已得非零 `codereg=0x1840FFE40` / `metareg=0x185BC8850`；同 build metadata 哈希 Win≠Mac → **禁止** Win interop 直拷 Mac；PE 虚址 ≠ Mac RVA。当时文案写「R4 不升格」——**已被 A3 废止**。  
> - 2026-10-19 **A2 Task 2.5-A**：有界 RE 加深 → **阴性/BLOCKED**。证据 `docs/evidence/interop-runtime-registration-x64-task2-re-a25-negative.txt`。  
> - 2026-10-19 **A2 Task 2**：地图启发定位 → **阴性/BLOCKED**（双侧 0）。证据 `docs/evidence/interop-runtime-registration-x64-task2-a2-negative.txt`。  
> - 2026-10-19 **A3**（本修订）：**双轨并行**。轨 A = 继续 Mac reg RE → Cpp2IL → Il2CppInterop（demo建造；Task 2 / 2.5-A 阴性**允许继续收窄，不因并行停**）。轨 B = 原 **R4 升格**为并列主路径 — IL2CPP C API 探针（**demo建造-CAPI**）。通过 = 共用硬门槛 + **至少一轨**额外条款；门禁须标通过轨（A / B / 双通）。两轨**互不因对方阴性停工**。多选 Task 5–9 仍停；Win interop 禁直拷；废止 `0x34D4F68` 仍有效。

---

## Goal

在用户本机 **Steam 原生 Mac** Two Point Museum（AppID `2185060`，Unity `6000.0.63f1`，metadata v31，buildid `25799969`）上，**证明**进程内能读到游戏托管类型/字段（调用桥）：

- **轨 A（demo建造）：** Win PE 对照地图启发 → Mac 非零 `codereg`+`metareg` → Cpp2IL（Mac 切片）→ Il2CppInterop → 托管探针打日志。  
- **轨 B（demo建造-CAPI）：** `nm`/`dlsym` 确认 `il2cpp_*` 导出 → P/Invoke 薄封装 → 至少一次 `il2cpp_class_from_name`（或文档等价）非空并打 BepInEx 日志。  

**一句话：** 先搞通「能调游戏 C# 类型」，再谈多选。验收对齐规格 §4 **A3**：共用硬门槛 + 至少一轨额外。Win 离线成功**不**单独通过；仅 Loader 启动不算通过。

---

## Architecture

**结论先行：** A 侧 Loader 已通且保持不动；废止 `0x34D4F68`；**R4 = 轨 B（已升格并行）**，不再是「失败后才启动的回退」。两轨并行、互不停工；任一轨先满足「共用硬门槛 + 本轨额外」即可标本规格调用桥通过。

```
┌──────────────────────────────────────────────────────────────────┐
│  已通（不重做）— Mac Museum Mod Loader（loader/）                   │
│  BepInEx be.789+6cea286；UpdateInteropAssemblies=false（保持）     │
└─────────────────────────────┬────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────────┐   ┌─────────────────────────────┐
│  轨 A — demo建造              │   │  轨 B — demo建造-CAPI         │
│  plugins/interop-probe/     │   │  plugins/capi-probe/（新建） │
│                             │   │                             │
│  Task0 共用基线              │   │  Task0 共用基线（自检）        │
│  TaskA1 Win PE 对照地图      │   │  TaskB1 nm/dlsym 导出确认    │
│  TaskA2 Mac reg 定位（可继续）│   │  TaskB2 P/Invoke 薄封装      │
│  TaskA3 Cpp2IL（Mac）        │   │  TaskB3 class_from_name 探针 │
│  TaskA4 Il2CppInterop        │   │  TaskB4 evidence + 门禁标 B  │
│  TaskA5 托管调用探针         │   │                             │
│  TaskA6 复现清单（轨 A）      │   │  （可与轨 A 并行；互不停）     │
└─────────────────────────────┘   └─────────────────────────────┘
              │                               │
              └───────────┬───────────────────┘
                          ▼
              共用硬门槛：进程内解析任意游戏类型/字段
              （或 C API 等价 Il2CppClass*/类型名）+ LogOutput
              + 可复现 + 工程落点本机 + 无 CrossOver / 无 Denuvo
                          │
                          ▼
              通过 = 共用硬门槛 + (轨A额外 | 轨B额外 | 双通)
              门禁必须标明：通过轨 = A / B / 双通

停用：plugins/multiselect/ 玩法 Task 5–9
废止：CodeReg RVA 0x34D4F68；磁盘启发式 0x34CC0D0
禁令：Win interop 直拷 Mac；PE 虚址当 Mac RVA
回退：R1 进程内优先；R2 XrefScan 子问题（轨 A）；R3=对照地图（辅助）；R4=轨 B（已升格）
```

**仓库结构（唯一代码落点 = 本机；相对既有布局增量）：**

```
/Users/weidu/Documents/Project/TPM-muti-select/
├── docs/
│   ├── specs/tpm-mac-il2cpp-interop-spec.md
│   ├── plans/tpm-mac-il2cpp-interop-plan.md   # 本文件
│   ├── research/tpm-mac-il2cpp-interop-research.md
│   ├── task4-interop-gate.md
│   ├── interop-runtime-gate.md               # Task0 + 双轨门禁叙述；须标通过轨
│   ├── evidence/
│   │   ├── win-depot-r3-notes.md
│   │   ├── win-depot/cpp2il-win-pe.log
│   │   ├── interop-runtime-registration-*-negative.txt  # 轨 A 史实阴性
│   │   ├── capi-exports-*.txt / capi-class-from-name-*.log  # 轨 B
│   │   └── …
│   └── …
├── loader/                                   # A：已通；不改
├── plugins/
│   ├── multiselect/                          # Task 5–9 停
│   ├── interop-probe/                        # 轨 A（demo建造）
│   └── capi-probe/                           # 轨 B（demo建造-CAPI；新建）
└── tools/
    ├── re/                                   # 轨 A 有界 RE（可选）
    └── interop-runtime/                      # 可复现清单/脚本
```

**Win depot（对照只读，勿复制大文件进仓库/box）：**

```
/Users/weidu/Documents/TPM-win-depot/
├── GameAssembly.dll
└── TPM_Data/il2cpp_data/Metadata/global-metadata.dat
```

**模块边界**

| 轨 A（interop-probe / tools） | 轨 B（capi-probe） | docs/evidence | 禁止 |
|-------------------------------|--------------------|---------------|------|
| 进程内定位 Mac registration；写 evidence | `Il2CppApiExports` + `nm`/`dlsym` 记录 | Win 地图、哈希、废止声明、双轨日志 | PE VA 当 Mac RVA；Win interop 直拷 |
| 地图启发扫描/校验 | `ClassFromNameProbe` 按名解析 | 门禁笔记；通过轨标注 | 多选玩法；CrossOver；Denuvo 对抗 |
| Cpp2IL/Il2CppInterop/托管读 | P/Invoke 薄封装；不依赖 Il2CppInterop 生成物 | 导出清单；class_from_name 摘录 | 因对方轨阴性而停工 |
| 依赖已装 Loader | 依赖已装 Loader（独立插件 GUID） | 引用 task4 / A1–A2 阴性 | 无记录 ASLR 手填；改 Loader 钉死包 |

---

## Tech Stack

| 项 | 选择 | 理由 |
|----|------|------|
| Loader（沿用） | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | `loader/VERSION.txt`；Task 1 已通 |
| 配置现状 | `UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false` | 避免自动静态 Cpp2IL 卡死；轨 B 亦不依赖自动生成 |
| 游戏 | Steam 原生 Mac，AppID `2185060`，Unity `6000.0.63f1`，metadata **v31**，buildid **`25799969`** | 规格 + R3 笔记钉死 |
| GameAssembly (Mac) | universal Mach-O（x86_64 + arm64） | 静态分析两切片均失败；轨 B 对运行中 dylib `dlsym` |
| Win depot（对照，轨 A） | `/Users/weidu/Documents/TPM-win-depot/` | A2；仓库外只读 |
| Cpp2IL（轨 A） | **≥** `2022.1.0-pre-release.21` | Mac 手动地址；换构建须记版本+结果 |
| Il2CppInterop（轨 A） | 与所用 BepInEx BE 捆绑版本一致 | 注意 #1109 XrefScan |
| 轨 A 探针 | `plugins/interop-probe/`，`net6.0`，`AllowUnsafeBlocks` | 已有脚手架 |
| 轨 B 探针 | `plugins/capi-probe/`，`net6.0`，`AllowUnsafeBlocks` | **新建**；独立 BepInEx 插件 |
| 基址 / 符号 | `dlopen` / `dladdr` / `dlsym`；磁盘侧 `nm -gU` | Il2CppInterop #206；轨 B 硬依赖 |
| 对照反汇编（轨 A 可选） | Capstone / Ghidra / Hopper / IDA / REA MCP | A1 已用；有界 |
| 架构 | 主验证 = Task0 锁定实机切片（史实：**x64 Rosetta**）；arm64 对照可选 | 生成/运行/导出解析同切片 |
| 代码落点 | `/Users/weidu/Documents/Project/TPM-muti-select` | 唯一 |

---

## Global Constraints

1. **唯一开发落点：** `/Users/weidu/Documents/Project/TPM-muti-select`。禁止把主代码工作区放在 box `/workspace` 或其它远程机。文档权威 = 本机 `docs/`；`/workspace/demo-lab/` 仅可选镜像。
2. **双轨并行、互不停工：** 轨 A 与轨 B **同时推进**。一轨阴性/BLOCKED **不得**作为另一轨停工理由。TaskA2 / 2.5-A 史实阴性 → 轨 A **允许继续收窄**，不因 A3 并行而停。
3. **通过规则（规格 §4 A3）：**  
   - **共用硬门槛（必须）：** 进程内解析到任意游戏类型/字段（或 C API 等价 `Il2CppClass*` / 类型名）并写入 BepInEx 日志；步骤可复现；工程落点本机仓库；不换 CrossOver、不对抗 Denuvo。  
   - **轨 A 额外：** 非零 Mac `codereg`+`metareg` → evidence；Cpp2IL→Il2CppInterop 无致命错。  
   - **轨 B 额外：** 所用 `il2cpp_*` 导出经 `nm`/`dlsym` 记录；至少一次按名解析非空并打日志。  
   - **通过 = 共用硬门槛 + 至少一轨额外**；门禁须标明 **通过轨 = A / B / 双通**。另一轨未完**不阻断**调用桥通过。
4. **A2 对照地图（轨 A 辅助，非捷径）：** 允许用 Win PE 提炼可迁移签名/初始化模式指导 Mac 定位。**禁止**：PE 虚址当 Mac RVA；Win Dummy/Il2CppInterop 直拷 Mac；假设 metadata 字节同源（已证哈希不一致）。
5. **废止地址：** `0x34D4F68`（及其派生绝对 VA）**禁止**再当 CodeReg 主候选或喂 Cpp2IL；仅可作反例。磁盘启发式 `0x34CC0D0` 同属不可采信。
6. **R4 = 轨 B（已升格并行）：** 原「失败后才启动的 C API 回退」**废止**。C API 为规格 A3 并列主验收路径，由 **demo建造-CAPI** 执行。R1/R2/R3 叙述见失败回退表。
7. **不做：** 多选玩法；CrossOver / Wine / Proton；Denuvo 对抗；无限扩大 RE 范围（超出 A1/A2 有界默许须另开规格修订）。
8. **Loader A 已通：** 不重做；保持钉死包与 delayed Execute；维持 `UpdateInteropAssemblies=false`（轨 A 离线产物就位前；轨 B 全程不依赖自动生成）。
9. **静态 Cpp2IL / A1–A2 阴性 = 前提（轨 A）：** 禁止以「再跑一遍无手动地址的静态搜」或「重采已证伪 `0x34D4F68`」冒充进度。
10. **多选 Task 5–9 保持停。**
11. **ASLR：** 流程必须可复现（相对基址 / 符号名 + 清单）。禁止无记录单次手填。
12. **优先进程内；** 外部 lldb / 有界反汇编仅在授权边界内；SIP 挡外部 → **R1**（不影响轨 B 的 `dlsym` 进程内路径）。
13. **证据：** `docs/evidence/`；门禁叙述 `docs/interop-runtime-gate.md`。Win depot 大文件**不**拷入仓库。
14. **开放问题默认决策（无 TBD）：**  
    - 实机架构：以 Task0 锁定的当前启动路径为准（史实 x64 Rosetta）。  
    - 轨 A 定位：地图启发 → 进程内校验 → 必要时有界 RE（A1 边界）。  
    - 轨 B 最小类型：任意稳定无害类型名即可（如 `System.String` 或游戏内已知类名）；不要求选择/建造 API。  
    - Il2CppInterop 撞 #1109 → **R2**（仅轨 A）；不得绕 Denuvo；不得用 Win 产物冒充。  
    - 一轨先通过 → 另一轨可择机收束或继续作长期投资；**不**强制双通。

---

## 核心类型 / 数据形状（types-first）

### 共用 / 废止

```csharp
// 废止常量（两轨均不得 ACCEPT 为成功）
static class DeprecatedCandidates {
  public const ulong FalsifiedCodeRegRva = 0x34D4F68UL;   // A1 假阳性
  public const ulong StaticDiskCodeRegGuess = 0x34CC0D0UL; // task4 磁盘启发式
}
```

### 轨 A — `plugins/interop-probe/`（既有 + 续作）

```csharp
readonly struct WinRegistrationMap {
  string BuildId;                   // "25799969"
  ulong  WinCodeRegistrationVa;     // 0x1840FFE40 — PE VA ONLY
  ulong  WinMetadataRegistrationVa; // 0x185BC8850 — PE VA ONLY
  string WinMetadataSha256;         // de8c2aa4…；≠ Mac
  string MacMetadataSha256;         // 071ad8ac…；≠ Win
}

readonly struct PortableRegHints {
  string SummaryMarkdownPath;       // docs/evidence/interop-win-map-portable-hints.md
}

interface IRegistrationLocator {
  bool TryLocate(PortableRegHints? hints, out RegistrationAddresses addrs, out string diagnostics);
}

readonly struct RegistrationAddresses {
  IntPtr GameAssemblyBase;
  ulong  CodeRegistrationVa;        // Mac；evidence 记 VA + RVA
  ulong  MetadataRegistrationVa;
  string SliceArch;                 // "x64" | "arm64"
}

static class RegistrationEvidenceWriter {
  static void Write(RegistrationAddresses a, string runId);
}

static class MinimalManagedCallProbe {
  static void ProbeAndLog();        // 轨 A：经 Il2CppInterop 读类型/字段
}
```

### 轨 B — `plugins/capi-probe/`（新建；demo建造-CAPI）

```csharp
/// <summary>磁盘 nm + 进程内 dlsym 确认的导出表；写入 evidence。</summary>
sealed class Il2CppApiExports {
  public IntPtr GameAssemblyHandle { get; }   // dlopen
  public IntPtr Il2CppDomainGet { get; }
  public IntPtr Il2CppDomainGetAssemblies { get; } // 可选
  public IntPtr Il2CppClassFromName { get; }   // 必需
  public IntPtr Il2CppRuntimeInvoke { get; }  // 可选（后续扩展）
  public IntPtr Il2CppStringNew { get; }       // 可选
  // …按 TaskB1 实测裁剪；至少 class_from_name 非零

  public static bool TryResolve(out Il2CppApiExports exports, out string diagnostics);
}

/// <summary>P/Invoke / 函数指针薄封装；不生成 Il2CppInterop。</summary>
static class Il2CppNative {
  // 签名以 TaskB1 记录的导出为准；示意：
  // delegate* unmanaged[Cdecl]<byte*, byte*, IntPtr> il2cpp_class_from_name;
  // 或 DllImport("GameAssembly") — Mac 上优先 dlsym 函数指针，避免硬编码装载名踩坑
}

/// <summary>按名解析非空 Il2CppClass* 并打 BepInEx 日志。</summary>
static class ClassFromNameProbe {
  /// <param name="namespaze">命名空间，可空字符串表示 global</param>
  /// <param name="name">类型短名</param>
  /// <returns>非零指针则成功</returns>
  public static IntPtr TryResolveClass(Il2CppApiExports api, string namespaze, string name);
  public static void ProbeAndLog(Il2CppApiExports api);
  // 日志必须含：所用符号名、尝试的 (ns, name)、指针是否非空、十六进制指针或类型名摘要
}

static class CapiEvidenceWriter {
  static void WriteExports(string nmLogPath, string dlsymTableMarkdown);
  static void WriteClassProbe(string logExcerptPath);
}
```

离线工具边界（轨 A）：`tools/interop-runtime/` — Cpp2IL 手动地址（**Mac** 切片 + **Mac** metadata）、Il2CppInterop CLI → `BepInEx/interop/`。轨 B **不**依赖该离线链。

---

## Tasks

### Task 0 — 共用基线、停工确认、废止地址、双轨门禁框

**执行方：** demo建造 维护门禁正文；**demo建造-CAPI** 开工前必须自检本任务判据已满足（可读既有 `interop-runtime-gate.md`，缺项则补记或请 demo建造 补，**不**阻塞轨 B 搭脚手架）。

**Files:**
- Modify: `docs/interop-runtime-gate.md`（Task 0 节；追加 A3 双轨通过框与通过轨栏）
- Reference（只读）: `docs/task4-interop-gate.md`；`docs/evidence/task4-cpp2il-pre21-*.log`；A1/A2 阴性证据；`docs/evidence/win-depot-r3-notes.md`；规格 A3

**Interfaces:**
- Consumes: 规格 A1–A3
- Produces: 基线表 + 废止列表 + 多选停工 + **双轨通过定义**（门禁模板含「通过轨」空栏）

- [ ] **0.1** 确认/补全基线表：Loader `be.789+6cea286`、Unity `6000.0.63f1`、metadata v31、Mac metadata SHA-256 `071ad8ace29c6ef49f5f97b8585da134c8219f8d7d44efba4dae12fb00509036`、GameAssembly 路径、启动架构（史实 **x64 Rosetta**）、`UpdateInteropAssemblies=false`、buildid `25799969`。
- [ ] **0.2** 链接静态失败前提：`task4-interop-gate.md` + `task4-cpp2il-pre21-*.log`。声明：轨 A 禁止无手动地址静态搜作为主路径。
- [ ] **0.3** 明文确认：**多选计划 Task 5–9 保持停**；本计划 slug 独立。
- [ ] **0.4** **废止声明：** `0x34D4F68` = 假阳性，禁止喂 Cpp2IL / 标标准通过；`0x34CC0D0` 不可采信；A1/A2 Task2 阴性为史实，**不**阻止轨 A 继续收窄，**不**阻止轨 B。
- [ ] **0.5** 指针 A2 Win depot（对照地图，非直拷）与 A3：**R4 = 轨 B 已升格并行**；删除任何「R4 不升格」残留表述。
- [ ] **0.6** 门禁增加 **A3 通过框模板**：共用硬门槛 □；轨 A 额外 □；轨 B 额外 □；**通过轨：A / B / 双通 / 未通过**（执行结束时勾选）。

**完成判据：** `interop-runtime-gate.md` 含基线 + 停工 + 废止 + A3 通过框；无「R4 不升格」矛盾句。  
**失败回退：** 缺 Loader/游戏路径 → 两轨均停装载验证；先修 A 侧。轨 B 仍可写导出脚本对磁盘 `GameAssembly.dylib` 做 `nm`（TaskB1.1）。

---

## 轨 A — 执行方：demo建造（`plugins/interop-probe/`）

> 以下 TaskA* **仅** demo建造执行。轨 B 阴性/未开工 **不**构成停工理由。TaskA2 史实阴性后允许继续收窄。

### TaskA1 — Win PE 对照地图（可迁移签名 / 初始化模式）

**Files:**
- Create: `docs/evidence/interop-win-map-portable-hints.md`
- Create/Update: `docs/evidence/interop-win-map-summary.md`
- Modify: `docs/interop-runtime-gate.md`（轨 A / TaskA1 节）
- Reference（只读）: `/Users/weidu/Documents/TPM-win-depot/`；`docs/evidence/win-depot/cpp2il-win-pe.log`；`docs/evidence/win-depot-r3-notes.md`

**Interfaces:**
- Consumes: `WinRegistrationMap` 硬事实
- Produces: `PortableRegHints`；**不**产生 Mac RVA

**Win 侧硬事实：**

| 字段 | 值 |
|------|-----|
| buildid | `25799969` |
| Win `codereg` | `0x1840FFE40`（**PE VA**） |
| Win `metareg` | `0x185BC8850`（**PE VA**） |
| Win metadata SHA-256 | `de8c2aa40aa36188a2fe8e72bba84cff4a22cdc1edb071005ef19313491243d8` |
| Mac metadata SHA-256 | `071ad8ace29c6ef49f5f97b8585da134c8219f8d7d44efba4dae12fb00509036` |
| 哈希 | **不一致** → 禁止 Win interop 直拷 |

- [ ] **A1.1** `interop-win-map-summary.md` 记上表 + 三禁令：(a) 禁 PE VA→Mac RVA；(b) 禁 Win Dummy/interop 直拷；(c) 禁因 Win Binary init 勾选规格通过。
- [ ] **A1.2** 提炼 `PortableRegHints`：字段布局/计数校验、初始化双写模式、各自 type-count、Accept 谓词（排除 A1 cookie 反例）。
- [ ] **A1.3** 可选有界反汇编充实 hints；产物仍是文档。
- [ ] **A1.4** 门禁勾选「地图已就绪 → TaskA2」。

**完成判据：** 两份 evidence Markdown + 三禁令；无「`0x1840FFE40` 是 Mac 地址」表述。  
**失败回退：** Win depot 缺失 → 停轨 A 地图依赖步骤并请用户确认路径；**不**停轨 B。

---

### TaskA2 — 地图指导定位 Mac 非零 codereg+metareg

**Files:**
- Modify: `plugins/interop-probe/Registration/`（接受 hints；拒绝废止 RVA）
- Modify: `plugins/interop-probe/Evidence/RegistrationEvidenceWriter.cs`
- Create/Update: `docs/evidence/interop-runtime-registration-<arch>-<runId>.txt`
- Modify: `docs/interop-runtime-gate.md`
- Optional: `tools/re/`

**Interfaces:**
- Consumes: `PortableRegHints`；`dlopen`/`dladdr`
- Produces: `RegistrationAddresses`（双侧非零）→ 轨 A 额外条款之地址半段

- [x] **A2.1** Locator 接受 hints；拒绝 `0x34D4F68` / `0x34CC0D0` 作为 ACCEPT。（史实已做）
- [x] **A2.2** 定位优先级：进程内 hints 扫描 → 有界 RE → 外部 lldb（SIP→R1）。（史实已做一轮）
- [ ] **A2.3** 取得 **非零** Mac `codereg` **与** `metareg`；写入 evidence；可复现步骤入门禁。
- [ ] **A2.4** ASLR 自检：≥两次启动，相对基址可重复。
- [x] **A2.5** A2 轮次阴性已文档化（`*-task2-a2-negative.txt` / `*-task2-re-a25-negative.txt`）。**A3：** 允许继续收窄 RE / 换扫描谓词；**不得**因轨 B 并行或本轮阴性而永久停轨 A；也不得直拷 Win interop 冒充完成。

**完成判据：** Mac 双侧非零 + 可复现（轨 A 额外之地址）。  
**失败回退：** R1；继续收窄或文档化未完成；**不**阻断轨 B；不直拷 Win。

**史实（2026-10-19）：** A2.1/A2.2/A2.5 已做；**A2.3 未达成**（双侧 0）。A3 下本任务保持开放直至地址可得或用户另批停轨 A。

---

### TaskA3 — 手动地址跑 Cpp2IL → Dummy（Mac 切片）

**Files:**
- Evidence: `docs/evidence/interop-runtime-cpp2il-<arch>.log`
- Modify: `docs/interop-runtime-gate.md`
- Tools: Cpp2IL ≥ pre.21；输入 = TaskA2 地址 + **Mac** GameAssembly 切片 + **Mac** metadata

- [ ] **A3.1** 用 **Mac** 地址跑 Cpp2IL；禁止喂废止 RVA 或 Win PE VA。
- [ ] **A3.2** Binary init 成功；产出 Dummy（Mac metadata）。
- [ ] **A3.3** CLI 日志入 evidence；版本写入门禁。
- [ ] **A3.4**（可选）另一架构对照；失败不阻断主切片。

**完成判据：** Binary init + Dummy + 日志。  
**失败回退：** 回 TaskA2 校验；不用「仅 Win Dummy」冒充。

---

### TaskA4 — Il2CppInterop 生成

**Files:**
- Evidence: `docs/evidence/interop-runtime-il2cppinterop-<arch>.log`
- Output → 游戏 `BepInEx/interop/`
- Modify: `docs/interop-runtime-gate.md`

- [ ] **A4.1** 与 BE 捆绑一致的 Generator 跑通。
- [ ] **A4.2** 核心程序集可加载；无致命错；日志入 evidence。
- [ ] **A4.3** 安装到 `BepInEx/interop/`；保持 `UpdateInteropAssemblies=false`。
- [ ] **A4.4** 撞 #1109 → **R2** 子问题；可试选项/更新构建；不得绕 Denuvo；不得用 Win 生成物顶替。

**完成判据：** 轨 A 额外之生成半段；或 R2 诚实文档化且未谎报通过。

---

### TaskA5 — 最小托管调用探针（轨 A 交付硬门槛）

**Files:**
- Modify: `plugins/interop-probe/Managed/MinimalManagedCallProbe.cs`；`Plugin.cs`
- Evidence: `docs/evidence/interop-runtime-managed-call.log`
- Modify: `docs/interop-runtime-gate.md`

- [ ] **A5.1** 引用生成的 `Il2Cpp.*`，解析任意一游戏托管类型或字段。
- [ ] **A5.2** 写入 `LogOutput.log`（或同等 BepInEx 日志）。
- [ ] **A5.3** 摘录到 evidence；门禁勾选共用硬门槛 + 轨 A 额外；**通过轨**至少含 **A**（若轨 B 亦过则 **双通**）。

**完成判据：** 共用硬门槛经 Il2CppInterop 路径满足 + 轨 A 额外已满足。  
**失败回退：** 查切片一致性；不把失败归咎于轨 B。

---

### TaskA6 — 轨 A 可复现清单与工程边界

**Files:**
- Create/Update: `tools/interop-runtime/`；`docs/interop-runtime-gate.md`

- [ ] **A6.1** 清单：启动 → 取址 → Cpp2IL → Il2CppInterop → 部署 → 探针；含废止警告与禁 Win 直拷。
- [ ] **A6.2** 工程边界：`interop-probe/` 与 `loader/`、`multiselect/`、`capi-probe/` 分离；多选 Task 5–9 仍停。
- [ ] **A6.3** 按成功标准映射表勾选轨 A 列。

**完成判据：** 轨 A 复现步骤完整；映射表轨 A 列可勾。

---

## 轨 B — 执行方：demo建造-CAPI（`plugins/capi-probe/`）

> 以下 TaskB* **仅** demo建造-CAPI 执行。轨 A Task2 阴性 / Il2CppInterop 未出 **不**构成停工理由。本轨 **不**依赖 Cpp2IL / Il2CppInterop 生成物。R4 已升格为本轨，可立即开工（Task0 自检后）。

### TaskB1 — `il2cpp_*` 导出确认（nm + dlsym）

**Files:**
- Create: `plugins/capi-probe/TpmMacCapiProbe.csproj`
- Create: `plugins/capi-probe/Native/Il2CppApiExports.cs`
- Create: `docs/evidence/capi-exports-nm-<arch>.txt`
- Create: `docs/evidence/capi-exports-dlsym-<arch>.md`
- Modify: `docs/interop-runtime-gate.md`（轨 B / TaskB1 节）

**Interfaces:**
- Consumes: 本机 `GameAssembly.dylib`（磁盘 + 进程内）；Task0 基线架构
- Produces: `Il2CppApiExports` 可解析；导出清单 evidence（轨 B 额外之「导出确认」）

- [ ] **B1.1** 对 Task0 锁定的实机切片对应的 `GameAssembly.dylib` 跑 `nm -gU`（或 `nm -g` 过滤），检索至少：`il2cpp_class_from_name`、`il2cpp_domain_get`、`il2cpp_runtime_invoke`（后两者可标「可选/未找到」）。完整相关行写入 `docs/evidence/capi-exports-nm-<arch>.txt`。
- [ ] **B1.2** 新建 `plugins/capi-probe/`：`net6.0`、`AllowUnsafeBlocks`、BepInEx IL2CPP 插件包引用（与 Loader 钉死 BE 一致）；**独立** `BepInPlugin` GUID（勿与 `interop-probe` / `multiselect` 冲突）。
- [ ] **B1.3** 实现 `Il2CppApiExports.TryResolve`：`dlopen` GameAssembly → `dlsym` 逐符号；任一**必需**符号（至少 `il2cpp_class_from_name`）为 0 则失败并写 diagnostics。
- [ ] **B1.4** 冷启动游戏 + Loader，插件日志打印各符号地址；将表格写入 `docs/evidence/capi-exports-dlsym-<arch>.md`（符号名、非零否、指针十六进制）。
- [ ] **B1.5** 门禁勾选：轨 B 额外「导出确认」□（nm 文件存在 + dlsym 表显示 `il2cpp_class_from_name` 非零）。

**完成判据：** nm 证据 + 进程内 `il2cpp_class_from_name` dlsym 非零；`Il2CppApiExports` 可构造。  
**失败回退：** 符号剥离/改名 → 记录 evidence 并停轨 B 后续，**不**停轨 A；可尝试 `dlsym` 常见别名并文档化，禁止猜地址硬编码。

**示例命令（B1.1）：**

```bash
# 将 DYLIB 换成 Task0 门禁中的绝对路径；架构与实机一致时先 lipo -thin 再 nm
nm -gU "$GAMEASSEMBLY_DYLIB" 2>/dev/null | rg -i 'il2cpp_(class_from_name|domain_get|runtime_invoke|string_new)' \
  | tee docs/evidence/capi-exports-nm-x64.txt
```

**示意实现片段（B1.3 — 工程师按实机改库名/符号）：**

```csharp
// plugins/capi-probe/Native/Il2CppApiExports.cs
sealed class Il2CppApiExports {
  public IntPtr ClassFromName { get; private init; }
  public IntPtr DomainGet { get; private init; }

  public static bool TryResolve(out Il2CppApiExports e, out string diag) {
    e = null!; diag = "";
    var h = dlopen("GameAssembly", RTLD_NOW); // 或绝对路径；以实机为准
    if (h == IntPtr.Zero) { diag = "dlopen failed"; return false; }
    var cfn = dlsym(h, "il2cpp_class_from_name");
    if (cfn == IntPtr.Zero) { diag = "dlsym il2cpp_class_from_name=0"; return false; }
    e = new Il2CppApiExports {
      ClassFromName = cfn,
      DomainGet = dlsym(h, "il2cpp_domain_get"), // 可为 Zero（可选）
    };
    return true;
  }
  // DllImport libc: dlopen/dlsym/dlerror …
}
```

---

### TaskB2 — P/Invoke / 函数指针薄封装

**Files:**
- Create: `plugins/capi-probe/Native/Il2CppNative.cs`
- Create: `plugins/capi-probe/Native/NativeDelegates.cs`（或同文件）
- Modify: `plugins/capi-probe/Plugin.cs`（加载时解析导出）
- Test/手动: 启动后无崩溃；日志打印「native wrappers ready」

**Interfaces:**
- Consumes: `Il2CppApiExports`
- Produces: 可调用的 `ClassFromName(byte* ns, byte* name) -> IntPtr` 封装

- [ ] **B2.1** 定义非托管函数指针委托（Cdecl），至少覆盖 `il2cpp_class_from_name`。签名以 IL2CPP 头/开源惯例为准：`(const char* namespaze, const char* name) -> Il2CppClass*`。
- [ ] **B2.2** 从 `Il2CppApiExports` 地址构造委托 / `delegate*`；**禁止**把 Win PE 地址或废止 RVA 写入封装。
- [ ] **B2.3** `Plugin.Load`/`Awake`：`TryResolve` → 包装就绪日志；失败则 Error 日志并跳过探针（不拖垮 Loader）。
- [ ] **B2.4** 提交工程可编译；不引用轨 A 的 Il2CppInterop 生成 DLL。

**完成判据：** 工程编译通过；运行时日志显示 wrappers ready（或明确的 Resolve 失败 diagnostics）。  
**失败回退：** 调用约定不对导致崩 → 缩到只 dlsym 不 invoke，修约定后再试；记录门禁；不停轨 A。

**示意：**

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
unsafe delegate IntPtr Il2CppClassFromNameFn(byte* namespaze, byte* name);

static class Il2CppNative {
  public static Il2CppClassFromNameFn ClassFromName = null!;
  public static void Bind(Il2CppApiExports e) {
    ClassFromName = Marshal.GetDelegateForFunctionPointer<Il2CppClassFromNameFn>(e.ClassFromName);
  }
}
```

---

### TaskB3 — `il2cpp_class_from_name` 非空探针 + 日志（轨 B 硬门槛）

**Files:**
- Create: `plugins/capi-probe/Probe/ClassFromNameProbe.cs`
- Modify: `plugins/capi-probe/Plugin.cs`
- Evidence: `docs/evidence/capi-class-from-name-<arch>.log`
- Modify: `docs/interop-runtime-gate.md`

**Interfaces:**
- Consumes: `Il2CppNative` / `Il2CppApiExports`
- Produces: 非空 `Il2CppClass*`（或可打印类型名）+ BepInEx 日志 → **共用硬门槛** + 轨 B 额外「按名解析」

- [ ] **B3.1** 实现 `ClassFromNameProbe.ProbeAndLog`：至少尝试一组稳定目标。推荐顺序：  
  1. `("System", "String")` 或 `("System", "Object")`（若游戏 il2cpp 导出对 BCL 可见）；  
  2. 若 1 为空，改用游戏内无害已知类名（须在门禁写明来源：metadata 浏览 / 既有研究，**不**要求选择/建造 API）。  
- [ ] **B3.2** 调用 `il2cpp_class_from_name`；**非空**则 Info 日志：`[CAPI] class_from_name ok ns=… name=… ptr=0x…`；空则 Warning 并试下一候选；全部空 → 本任务未完成。
- [ ] **B3.3** 将 LogOutput 相关摘录复制到 `docs/evidence/capi-class-from-name-<arch>.log`。
- [ ] **B3.4** 门禁勾选：共用硬门槛 □；轨 B 额外「按名解析」□；若轨 A 未过则 **通过轨 = B**。

**完成判据：** 至少一次按名解析非空 + 日志 + evidence；轨 B 额外两条均满足并覆盖共用硬门槛。  
**失败回退：** 全部候选为空 → 查 domain 是否已 init（可补 `il2cpp_domain_get` 等待/重试一次）；仍失败则文档化，**不**升格玩法、不绕 Denuvo；不停轨 A。

**示意：**

```csharp
static class ClassFromNameProbe {
  public static unsafe void ProbeAndLog() {
    foreach (var (ns, name) in new[] { ("System", "String"), ("System", "Object") }) {
      var nsUtf8 = Encoding.UTF8.GetBytes(ns + "\0");
      var nameUtf8 = Encoding.UTF8.GetBytes(name + "\0");
      fixed (byte* pNs = nsUtf8)
      fixed (byte* pName = nameUtf8) {
        var klass = Il2CppNative.ClassFromName(pNs, pName);
        if (klass != IntPtr.Zero) {
          Log.Info($"[CAPI] class_from_name ok ns={ns} name={name} ptr=0x{klass.ToInt64():X}");
          return;
        }
        Log.Warn($"[CAPI] null for {ns}.{name}");
      }
    }
    Log.Error("[CAPI] all class_from_name candidates returned null");
  }
}
```

---

### TaskB4 — 轨 B evidence 收束、复现清单、通过轨标注

**Files:**
- Create/Update: `docs/evidence/capi-probe-repro.md`（或写入 `interop-runtime-gate.md` 轨 B 节）
- Modify: `docs/interop-runtime-gate.md`（勾选 A3 通过框）

**Interfaces:**
- Consumes: TaskB1–B3 全部证据
- Produces: 可复现步骤；门禁 **通过轨** 含 B（或双通）

- [ ] **B4.1** 复现清单：确认 Loader → 部署 `capi-probe` → 启动 → 读 LogOutput → 核对 evidence 文件列表。
- [ ] **B4.2** 工程边界声明：`plugins/capi-probe/` 独立；未改 `loader/`；未恢复多选；未拷 Win interop。
- [ ] **B4.3** 填写 A3 通过框：共用硬门槛、轨 B 额外；**通过轨 = B**（若轨 A 同时满足则改为 **双通**，由先完成方或门禁维护方更新）。
- [ ] **B4.4** 注明：轨 A 未完**不阻断**本规格调用桥通过（规格 A3）。

**完成判据：** 轨 B 证据齐全；门禁通过轨已标；复现步骤可交给他人重跑。  
**失败回退：** 缺摘录 → 补拷日志，不算 TaskB4 完结。

---

## 失败回退表（规格 §6.3 + A3）

| 顺序 | 回退 | 触发 | 本计划状态 |
|------|------|------|------------|
| **R1** | 改进程内探针（放弃被 SIP 挡住的外部 lldb） | 外部调试不可用 | 保留（主要影响轨 A） |
| **R2** | 记录「地址已得但 Il2CppInterop 生成失败」并开子问题（XrefScan 等） | 类似 BepInEx #1109 | 保留（**仅轨 A**） |
| **R3** | Win PE 对照地图指导 Mac 定位；Win 离线 Cpp2IL **不**单独通过 | A2 辅助手段 | **已启用（地图，非捷径）** |
| **R4** | IL2CPP C API 探针 | 原「托管 interop 不可行时回退」 | **A3 已升格为轨 B（并行）**；由 **demo建造-CAPI** 执行；不再是失败后才启动 |

**另一轨未完不阻断通过：** 共用硬门槛 + 单轨额外即可；门禁必须写明通过轨。

---

## 风险与回退（摘要）

| 风险 | 缓解 |
|------|------|
| 误用废止 `0x34D4F68` / `0x34CC0D0` | Task0.4 + Locator 拒绝；轨 B 禁止作 dlsym 替代 |
| 误把 Win PE VA 当 Mac RVA / 直拷 interop | TaskA1 三禁令；轨 B 不引用 Win 生成物 |
| 因一轨阴性停另一轨 | Global Constraints §2；分工表 |
| 残留「R4 不升格」文案 | Task0.5；本变更记录 A3 |
| SIP 挡外部调试 | R1；轨 B 走进程内 dlsym |
| ASLR | 相对基址 / 符号名流程 |
| `Process.Modules` 不见 dylib（#206） | dlopen/dladdr/dlsym |
| Il2CppInterop #1109 | R2；不影响轨 B 通过 |
| `class_from_name` 全空 | 等 domain init；换候选类名；文档化 |
| 符号剥离 | nm 先证；失败诚实停轨 B |
| 误恢复多选 / 改 Loader | Task0.3；两轨工程边界 |
| 游戏小版本更新 | 门禁钉 Unity/buildid；失效重跑相应轨 |

---

## 成功标准映射自检（A3 双轨）

| 条款 | 规格要求 | 轨 A 任务 | 轨 B 任务 | 证据落点 |
|------|----------|-----------|-----------|----------|
| **共用硬门槛** | 进程内解析任意游戏类型/字段（或 C API 等价）并打日志 | **TaskA5** | **TaskB3** | `interop-runtime-managed-call.log` **或** `capi-class-from-name-*.log` + LogOutput |
| 共用：可复现 | 脚本或清单可复现 | TaskA6.1 | TaskB4.1 | 门禁 / `tools/interop-runtime/` / `capi-probe-repro` |
| 共用：工程落点 | 本机仓库清晰单元 | `plugins/interop-probe/` | `plugins/capi-probe/` | 仓库树 |
| 共用：约束 | 无 CrossOver / 无 Denuvo | 全局 + TaskA6 | 全局 + TaskB4.2 | `interop-runtime-gate.md` |
| **轨 A 额外** | 非零 Mac codereg+metareg → evidence | **TaskA2** | — | `interop-runtime-registration-*` |
| **轨 A 额外** | Cpp2IL→Il2CppInterop 无致命错 | **TaskA3–A4** | — | `interop-runtime-cpp2il-*` / `il2cppinterop-*` |
| **轨 B 额外** | 所用 `il2cpp_*` 经 nm/dlsym 记录 | — | **TaskB1** | `capi-exports-nm-*` / `capi-exports-dlsym-*` |
| **轨 B 额外** | 至少一次按名解析非空并打日志 | — | **TaskB3** | `capi-class-from-name-*` |
| **通过定义** | 共用 + 至少一轨额外；标通过轨 | TaskA5.3 / A6.3 | TaskB3.4 / B4.3 | 门禁 **通过轨 = A / B / 双通** |

**辅助（非独立成功条）：** Task0 废止与双轨框；TaskA1 Win 地图。

**明确不算成功：** 仅插件加载；仅 metadata 可读；仅 Win PE Cpp2IL 成功；仅 Win interop 拷入 Mac；勾选废止地址作为轨 A 地址额外。

---

## 与多选计划 / 史实进度的关系

| 项 | 状态 |
|----|------|
| `tpm-mac-multiselect` Task 0–3 | 已完成史实；只引用 |
| `tpm-mac-multiselect` Task 4 | **失败**；互操作由本规格/计划接管 |
| `tpm-mac-multiselect` Task **5–9** | **保持停** |
| 轨 A 旧脚手架 / A2 Task2 | 可复用工程；`0x34D4F68` 不得沿用；A2.3 未达成，A3 下继续 |
| A1 有界反汇编 | **阴性**；反例 |
| R3 Win depot | 地图原料；不跳过 Mac 验收 |
| **R4 / 轨 B** | **A3 升格并行**；demo建造-CAPI 独立任务链 TaskB1–B4 |
| 本规格通过后 | 再开多选规格修订或恢复玩法（非本计划范围） |

---

## 待澄清（规格未钉死处 — 不瞎编）

| 项 | 现状 | 计划默认 |
|----|------|----------|
| Mac 实机主切片是否永久钉 x64 Rosetta | 门禁史实为 x64 | Task0 锁定**当前**启动路径；改架构须更新门禁并重跑相应轨 |
| 轨 B 首选 `(ns, name)` 若 BCL 不可见 | 规格未钉类名 | TaskB3.1 候选列表 + 门禁写明来源；任意无害类型即可 |
| `il2cpp_runtime_invoke` 是否本 demo 必需 | 规格举例含 invoke | **最小通过不强制 invoke**；class_from_name 非空即满足轨 B 额外「按名解析」；invoke 可作 B3 后可选增强 |
| PortableRegHints 最小充分集 | 规格未钉字段级模板 | TaskA1.2 四类线索为最低交付 |
| 两插件是否允许同进程同时加载 | 未钉 | **允许**；GUID 分离；互不依赖；避免争用同一 evidence 文件名（前缀 `interop-` vs `capi-`） |
| REA / Ghidra 是否必用 | 规格 A1：可选 | 轨 A 不强制；轨 B 不需要 |

---

## 相对 A2 的主要 diff（摘要）

1. **双轨并行** + 执行分工表（demo建造 / demo建造-CAPI）。  
2. **R4 升格为轨 B**；删除「R4 不升格」；回退表更新。  
3. **成功标准**改为共用硬门槛 + 至少一轨额外；通过轨标注。  
4. 任务编号改为 **Task0 共用 + TaskA\* + TaskB\***；轨 B 为完整可独立勾选链 B1→B4。  
5. 新增 `plugins/capi-probe/` 与类型 `Il2CppApiExports` / `ClassFromNameProbe`。  
6. Global Constraints 增加并行互不停工；轨 A 阴性允许继续收窄。  
7. 多选停、废止地址、Win 禁直拷、Loader/`UpdateInteropAssemblies=false` 保留。

---

*计划结束（A3 修订）。轨 A 由 **demo建造** 执行；轨 B 由 **demo建造-CAPI** 执行。两轨可同时开工（Task0 自检后）。文内无用户执行方式提问；批准前若需用户确认以父代理流程为准。*
