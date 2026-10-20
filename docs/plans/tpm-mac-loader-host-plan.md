# Plan: Two Point Museum — Mac Mod Loader 宿主完善（tpm-mac-loader-host）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **依据规格已批准：** 2026-10-19（Asia/Shanghai）  
> **主题 slug：** `tpm-mac-loader-host`（**独立于** `tpm-mac-multiselect` / `tpm-mac-il2cpp-interop`）  
> **切片：** **host-plus-smoke**（宿主 + 多选声明依赖并冒烟；**不开**玩法）  
> **方案：** **①** 平台插件（C API Host）随 Loader 交付  
> **文档权威落点（本机仓库）：** `/Users/weidu/Documents/Project/TPM-muti-select/docs/`  
> **规格：** `docs/specs/tpm-mac-loader-host-spec.md`  
> **计划：** `docs/plans/tpm-mac-loader-host-plan.md`（本文件权威副本）  
> **前因：** 轨 B 已通过（`docs/interop-runtime-gate.md` 通过轨=B；`plugins/capi-probe/` + `docs/evidence/capi-*`）  
> **可选镜像：** `/workspace/demo-lab/`（非权威；有改动时以本机 `docs/` 为准并回写）  
> **若规格变更：** 必须同步更新本计划，并在本段追加变更记录（日期 + 条目）。  
>
> **执行分工（规格 §9 钉死）**  
> | 角色 | 执行方 | 工程落点 | 主交付 |  
> |------|--------|----------|--------|  
> | **主执行** | **demo建造-CAPI** | `plugins/mac-il2cpp-host/`、`loader/scripts/`、门禁 | Host 晋升、安装部署 Host、Loader+Host / 缺 Host 负例门禁 |  
> | **协助** | **demo建造** | `plugins/multiselect/`（仅依赖+冒烟） | Multiselect `BepInDependency` + Host Ready 冒烟日志；**同时继续轨 A**（`interop-probe/`） |  
> | 不职责 | 任一建造 | — | **不得**借机恢复多选玩法 Task 5–9；**不得**停轨 A |  
>
> **变更记录**  
> - 2026-10-19：初版。规格已批准（host-plus-smoke / 方案①）。主路径 = `capi-probe` 晋升 `mac-il2cpp-host` → `install-loader.sh` 部署 Host → Multiselect 硬依赖 + 冒烟 → `loader-host-gate.md` 三场景。轨 A 不停；玩法不停恢复。

---

## Goal

在用户本机 **Steam 原生 Mac** Two Point Museum（AppID `2185060`，Unity `6000.0.63f1`，buildid `25799969`）上，把已证明的轨 B C API 探针**产品化**为随 Loader 默认交付的稳定 Host 插件，并让 Multiselect **硬依赖** Host、冒烟可观测、缺 Host **不静默生效**。

**一句话：** Loader 装完即有 Host；多选只声明依赖并打冒烟日志；不开 Shift 玩法；不停轨 A。

---

## Architecture

**结论先行：** 轨 B 探针已通 → 本切片只做**晋升 + 安装链 + 依赖契约**，不重做 `dlsym`/`class_from_name`。Host 源码在 `plugins/mac-il2cpp-host/`；Loader 安装脚本只拷构建产物；`capi-probe` 留作证据遗物、**不再**默认安装。Multiselect 加 `BepInDependency(Host GUID)` + Ready/smoke 日志；玩法桥与 Shift 逻辑本切片零接线。

```
┌──────────────────────────────────────────────────────────────────┐
│  Loader（loader/）— 已通；本切片仅完善安装路径                      │
│  BepInEx be.789+6cea286；delayed Execute；UpdateInterop=false     │
│  install-loader.sh → 部署 mac-il2cpp-host.dll → BepInEx/plugins/  │
│  （不部署 capi-probe；不把 Host 源码打进 loader/）                  │
└─────────────────────────────┬────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────────┐   ┌─────────────────────────────┐
│  Host — demo建造-CAPI         │   │  Multiselect 冒烟 — demo建造 │
│  plugins/mac-il2cpp-host/   │   │  plugins/multiselect/       │
│  GUID: com.tpm.mac.il2cpp-host│  │  GUID: com.tpm.mac.multiselect│
│  TryResolve + Ready         │◄──│  BepInDependency(Host)      │
│  ClassFromName（最小封装）   │   │  Host Ready / smoke 日志    │
│  Load 冒烟：非空 class 日志  │   │  无 Shift / 无玩法桥接线     │
└─────────────────────────────┘   └─────────────────────────────┘
              │                               │
              └───────────┬───────────────────┘
                          ▼
              门禁 docs/loader-host-gate.md
              ① Loader+Host 单独可用
              ② Host+Multiselect 依赖链
              ③ 缺 Host 负例（不静默生效）

并行不停：轨 A（plugins/interop-probe/）— demo建造
遗物保留：plugins/capi-probe/ + docs/evidence/capi-*（不默认安装）
停用：多选玩法 Task 5–9；方案②/③；Win interop 直拷；扩满 C API
```

**仓库结构（相对既有布局增量）：**

```
/Users/weidu/Documents/Project/TPM-muti-select/
├── docs/
│   ├── specs/tpm-mac-loader-host-spec.md
│   ├── plans/tpm-mac-loader-host-plan.md    # 本文件
│   ├── loader-host-gate.md                 # 新建（本切片门禁）
│   ├── evidence/loader-host-*.log|.md      # 三场景摘录
│   └── …
├── loader/
│   ├── VERSION.txt                         # 钉死包（不改版本号）
│   └── scripts/install-loader.sh           # 增：部署 Host；禁部署 capi-probe
├── plugins/
│   ├── mac-il2cpp-host/                    # 新建（自 capi-probe 晋升）
│   ├── multiselect/                        # 改：BepInDependency + 冒烟
│   ├── capi-probe/                         # 遗物；不默认安装
│   └── interop-probe/                      # 轨 A；本切片不改 GUID/职责
└── README.md                               # 两章更新
```

**模块边界**

| 模块 | 职责 | 不职责 |
|------|------|--------|
| `loader/` | 钉死包、软链、delayed Execute、**拷贝 Host DLL** | Host 源码；多选玩法；默认装 capi-probe |
| `plugins/mac-il2cpp-host/` | 稳定 GUID；`TryResolve`/`Ready`/`ClassFromName`；Load 冒烟日志 | 玩法；Il2CppInterop 生成；打进 BepInEx.Unity.IL2CPP.dll |
| `plugins/multiselect/` | `BepInDependency(Host)`；Ready/smoke 日志 | Shift / `IItemSelectionBridge` 接线；本切片玩法 |
| `plugins/capi-probe/` | 证据遗物 | 随 Loader 默认部署 |
| `plugins/interop-probe/` | 轨 A（并行） | 本规格门槛 |

---

## Tech Stack

| 项 | 选择 | 理由 |
|----|------|------|
| Loader 包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | `loader/VERSION.txt`；Task1 / 轨 B 已通 |
| 配置 | `UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false` | Host **不**依赖自动生成；轨 A 产物就位前保持 |
| 游戏 | Steam 原生 Mac，AppID `2185060`，Unity `6000.0.63f1`，buildid `25799969` | 规格钉死；变则重跑 Host 冒烟 |
| 启动架构 | **x64 Rosetta**（`arch -x86_64`） | 与 Task1 / 轨 B 一致；改 arm64 须另开修订 |
| Host 插件 | `plugins/mac-il2cpp-host/`，`net6.0`，`AllowUnsafeBlocks` | 自 `capi-probe` 晋升 |
| Multiselect | `plugins/multiselect/`，`net6.0` | 既有脚手架；仅加依赖+冒烟 |
| 符号解析 | `dlopen` / `dlsym` → 函数指针委托（沿用轨 B） | 已证 `il2cpp_class_from_name` 非空 |
| Host GUID | **`com.tpm.mac.il2cpp-host`** | 规格建议；**钉死进门禁** |
| Multiselect GUID | `com.tpm.mac.multiselect` | 保持不变 |
| 退役探针 GUID | `com.tpm.mac.capi-probe` | 不再默认加载 |
| 代码落点 | `/Users/weidu/Documents/Project/TPM-muti-select` | 唯一 |

---

## Global Constraints

1. **唯一开发落点：** `/Users/weidu/Documents/Project/TPM-muti-select`。文档权威 = 本机 `docs/`；`/workspace/demo-lab/` 仅可选镜像。
2. **切片边界：** 只做 host-plus-smoke。**禁止**实现 Shift 多选拾取/旋转/删除或接线 `IItemSelectionBridge` 玩法路径。
3. **方案①：** Host 随 Loader 默认交付。禁止方案②（三件套分装 Workshop）与方案③（Host 打进 `BepInEx.Unity.IL2CPP.dll` / Doorstop）。
4. **GUID 钉死：** Host = `com.tpm.mac.il2cpp-host`；Multiselect = `com.tpm.mac.multiselect`；探针 `com.tpm.mac.capi-probe` **不**默认安装。门禁必须写出 Host GUID 字符串。
5. **缺 Host 不静默生效：** Multiselect 在无 Host DLL / 依赖失败时，必须有可观测失败信号（BepInEx 依赖错误 **或** 插件自检 Error 日志，二者择一并写入门禁）。禁止「看起来加载了却无依赖声明」。
6. **安装脚本：** `install-loader.sh` 部署 Host 构建产物；**禁止**默认拷贝 `capi-probe` DLL。允许同机残留时文档警告双 GUID 双加载风险，脚本须避免同时部署。
7. **工程拆分：** Host 源码不进 `loader/` 二进制树；`loader/`、`mac-il2cpp-host/`、`multiselect/` 可分开构建。
8. **不停轨 A：** 本规格不依赖、不阻断 `tpm-mac-il2cpp-interop` 轨 A。demo建造继续 `interop-probe/`；互不改对方 GUID。
9. **不做：** CrossOver / Wine / Proton；Win interop 直拷 Mac；Denuvo 对抗；扩满 C API（`runtime_invoke` 等非本切片验收）；工坊上架。
10. **工具钉死：** Loader `be.789+6cea286`；x64 Rosetta；Unity `6000.0.63f1`；buildid `25799969`；AppID `2185060`。维持 `UpdateInteropAssemblies=false`。
11. **证据与门禁：** 新建 `docs/loader-host-gate.md` + `docs/evidence/loader-host-*`。三场景必过：Loader+Host；Host+Multiselect；缺 Host 负例。
12. **开放问题默认决策（无 TBD）：**  
    - Host 公开面：`TryResolve`/`Ready` + `ClassFromName`（image/ns/name 或封装到 image 解析）；冒烟类型优先 `System.String`（轨 B 已证）。  
    - Multiselect 引用 Host：编译期 ProjectReference **或** 仅 GUID 依赖 + 反射/类型探测；优先 **ProjectReference** 调 `IIl2CppHost`/`HostFacade`，若 BepInEx 加载顺序实测不足再加运行时自检。  
    - 缺 Host 负例：卸 Host DLL 后冷启动；接受 Chainloader 依赖失败日志 **或** Multiselect 显式 Error 后 return。  
    - `capi-probe`：保留目录与 evidence；README 标明遗物；安装脚本不拷。

---

## 核心类型 / 数据形状（types-first）

> 基于轨 B 已通实现（`Il2CppApiExports.TryResolve` / `Il2CppNative.Ready` / `ClassFromNameFn(image, ns, name)`）晋升；命名空间从 `TpmMacCapiProbe` 迁到 `TpmMacIl2CppHost`。下列为本切片**必须**对外稳定的面；辅助符号可保留但不扩验收。

### Host — `plugins/mac-il2cpp-host/`

```csharp
namespace TpmMacIl2CppHost;

public static class HostIds
{
    public const string Guid = "com.tpm.mac.il2cpp-host"; // 钉死；门禁原文核对
    public const string Name = "TPM Mac IL2CPP Host";
    public const string Version = "0.1.0";
}

/// <summary>对外最小可调用面；Multiselect / 日后玩法只依赖此契约。</summary>
public interface IIl2CppHost
{
    bool Ready { get; }
    /// <summary>
    /// 按 image + namespace + name 解析 Il2CppClass*。
    /// imageHint: null/空/"corlib" → il2cpp_get_corlib()；
    /// 其它字符串 → 在 domain assemblies 中按 image 名子串匹配（与轨 B 探针一致）。
    /// </summary>
    IntPtr ClassFromName(string? imageHint, string namespaze, string name);
}

/// <summary>进程内符号绑定；沿用轨 B TryResolve 语义。</summary>
public sealed class Il2CppApiExports
{
    public IntPtr Handle { get; }
    public IntPtr ClassFromName { get; }   // 必需非零
    public IntPtr GetCorlib { get; }       // 冒烟用；可 Zero 则走 assemblies 扫描
    // DomainGet / ClassGetName / … 辅助，不扩验收面

    public static bool TryResolve(out Il2CppApiExports exports, out string diagnostics);
}

public static class Il2CppNative
{
    public static bool Ready { get; }
    public static void Bind(Il2CppApiExports e);
    // ClassFromNameFn(IntPtr image, IntPtr ns, IntPtr name) 等委托 — 与轨 B 相同
}

/// <summary>门面：插件 Load 后写入；供同进程依赖方查询。</summary>
public static class HostFacade
{
    public static IIl2CppHost? Instance { get; private set; }
    public static bool Ready => Instance is { Ready: true };
    internal static void Set(IIl2CppHost host);
}

[BepInPlugin(HostIds.Guid, HostIds.Name, HostIds.Version)]
public class Il2CppHostPlugin : BasePlugin, IIl2CppHost
{
    public bool Ready { get; private set; }
    public IntPtr ClassFromName(string? imageHint, string namespaze, string name);
    public override void Load(); // TryResolve → Bind → HostFacade.Set → 冒烟 ClassFromName
}
```

**冒烟约定（Host Load）：** 至少一次成功解析（允许 `System.String`），日志前缀建议 `[HOST]`，含非空指针十六进制或 `got=System.String`。

### Multiselect 冒烟 — `plugins/multiselect/`（仅本切片增量）

```csharp
namespace TpmMacMultiselect;

// 保持
public const string PluginGuid = "com.tpm.mac.multiselect";

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(TpmMacIl2CppHost.HostIds.Guid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    public override void Load()
    {
        // 1) 日志：已加载
        // 2) 若 HostFacade.Ready → LogInfo "[MS] host ready / smoke ok"
        // 3) 若 HardDependency 未拦住且 !Ready → LogError 并 return（回退路径；满足「不静默生效」）
        // 禁止：接线 SelectionSet / ShiftInput / MultiselectController 玩法
    }
}
```

### 安装契约

```text
HOST_DLL_SRC = plugins/mac-il2cpp-host/bin/Release/TpmMacIl2CppHost.dll   # AssemblyName 以 csproj 为准
HOST_DLL_DST = "$GAME/BepInEx/plugins/TpmMacIl2CppHost.dll"
FORBIDDEN_DEFAULT = TpmMacCapiProbe.dll / *capi-probe*  — install-loader.sh 不得拷贝
```

---

## Tasks

### Task 0 — 基线锁定、废止「capi-probe 默认交付」、轨 A 不停声明

**执行方：** demo建造-CAPI（写门禁框）；demo建造 开工前自检可读。

**Files:**
- Create: `docs/loader-host-gate.md`（Task 0 节 + 三场景空栏模板）
- Reference（只读）: `loader/VERSION.txt`；`docs/task1-loader-smoke.md`；`docs/interop-runtime-gate.md`（通过轨=B）；`docs/evidence/capi-*`；规格 `tpm-mac-loader-host-spec.md`

**Interfaces:**
- Consumes: 规格 §4 / §6；轨 B 已通事实
- Produces: 基线表；「capi-probe 非默认交付」声明；轨 A 不停声明；门禁三场景空栏

- [ ] **0.1** 基线表写入 `loader-host-gate.md`：Loader `be.789+6cea286`（`VERSION.txt` 原文）；游戏路径默认 `$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum`；Unity `6000.0.63f1`；buildid `25799969`；AppID `2185060`；启动 **x64 Rosetta**；`UpdateInteropAssemblies=false`。
- [ ] **0.2** 链接轨 B 通过证据：`interop-runtime-gate.md` 通过轨=B；`docs/evidence/capi-class-from-name-x64.log`（或等价）证明 `il2cpp_class_from_name` → 非空。
- [ ] **0.3** **废止声明：** `capi-probe` / GUID `com.tpm.mac.capi-probe` **不再**作为 Loader 默认安装物；正式交付 GUID = `com.tpm.mac.il2cpp-host`。
- [ ] **0.4** 明文：**轨 A（`interop-probe/`）继续并行，本计划不阻断**；多选玩法 Task 5–9 **保持停**。
- [ ] **0.5** 门禁模板三场景空栏：① Loader+Host □；② Host+Multiselect □；③ 缺 Host 负例 □；Host GUID 核对栏（须手填 `com.tpm.mac.il2cpp-host`）。

**完成判据：** `loader-host-gate.md` 含基线 + 两废止/不停声明 + 三场景空栏；无「capi-probe 随 Loader 交付」表述。  
**失败回退：** 缺 Loader/游戏路径 → 先修 A 侧安装；Host 晋升源码仍可在仓库内进行。

---

### Task 1 — 新建 `mac-il2cpp-host`：自 `capi-probe` 晋升；钉 GUID；导出最小 API

**执行方：** demo建造-CAPI

**Files:**
- Create: `plugins/mac-il2cpp-host/TpmMacIl2CppHost.csproj`
- Create: `plugins/mac-il2cpp-host/Il2CppHostPlugin.cs`（或 `Plugin.cs`）
- Create: `plugins/mac-il2cpp-host/HostIds.cs`
- Create: `plugins/mac-il2cpp-host/IIl2CppHost.cs` / `HostFacade.cs`
- Create: `plugins/mac-il2cpp-host/Native/Il2CppApiExports.cs`（自 capi-probe 复制并改命名空间）
- Create: `plugins/mac-il2cpp-host/Native/Il2CppNative.cs`
- Create: `plugins/mac-il2cpp-host/Host/ClassFromNameService.cs`（封装 imageHint + 冒烟）
- Keep: `plugins/capi-probe/` **原样保留**（遗物；可加 README 一行「已被 mac-il2cpp-host 取代为默认交付」）

**Interfaces:**
- Consumes: 轨 B 已验证的 `TryResolve` / `Bind` / `class_from_name(image,ns,name)` 行为
- Produces: `HostIds.Guid`；`IIl2CppHost`；`HostFacade`；程序集 `TpmMacIl2CppHost.dll`

- [ ] **1.1** 新建 csproj：`net6.0`、`AllowUnsafeBlocks`、`AssemblyName=TpmMacIl2CppHost`；BepInEx HintPath 风格对齐 `capi-probe`（可用 `TpmGameRoot` / `HOME/.../Two Point Museum`）。
- [ ] **1.2** 复制并改名命名空间 `TpmMacCapiProbe` → `TpmMacIl2CppHost`；**GUID 改为** `com.tpm.mac.il2cpp-host`（禁止残留 `capi-probe` GUID）。
- [ ] **1.3** 导出 `IIl2CppHost` + `HostFacade`；`Il2CppHostPlugin.Load`：`TryResolve` → `Il2CppNative.Bind` → `Ready` → `HostFacade.Set(this)`。
- [ ] **1.4** 实现 `ClassFromName(imageHint, ns, name)`：corlib 优先；失败则 domain assemblies 扫描（逻辑对齐 `ClassFromNameProbe`，但作为可复用 API 而非一次性探针）。
- [ ] **1.5** `dotnet build -c Release` 成功；产物路径记入门禁（供安装脚本）。

**完成判据：** Release DLL 存在；源码内 GUID 字符串精确为 `com.tpm.mac.il2cpp-host`；`IIl2CppHost` 可被他项目引用编译。  
**失败回退：** 对照 `capi-probe` 逐符号 diff；不引入 Win PE / 废止 RVA；不改 Loader 钉死包。

---

### Task 2 — Host 冒烟：`ClassFromName` 非空日志

**执行方：** demo建造-CAPI

**Files:**
- Modify: `plugins/mac-il2cpp-host/Il2CppHostPlugin.cs`（Load 后冒烟）
- Create: `docs/evidence/loader-host-host-smoke-x64.log`（摘录）
- Modify: `docs/loader-host-gate.md`（场景①）

**Interfaces:**
- Consumes: `IIl2CppHost.ClassFromName`；`HostFacade.Ready`
- Produces: `[HOST]` 非空 class 日志；场景①勾选素材

- [ ] **2.1** Load 成功且 `Ready` 后调用 `ClassFromName(null, "System", "String")`（或 `"corlib"` hint）；非空则 `LogInfo` 含 ptr / `got=System.String`。
- [ ] **2.2** 手动将 Host DLL 放入 `$GAME/BepInEx/plugins/`（此时安装脚本可能尚未改完；允许临时 `cp`），冷启动 `./run_bepinex.sh`（x86_64）。
- [ ] **2.3** 确认 `LogOutput.log`：BepInEx 初始化 + Host 插件加载 + 至少一次非空 class 日志。摘录入 evidence。
- [ ] **2.4** 门禁场景①暂记「手工部署通过」；Task 3 脚本化后再复验「脚本部署通过」。

**完成判据：** evidence 日志含 Host GUID/名、Ready、非空 `class_from_name`（或 Host 封装等价）。  
**失败回退：** 对照轨 B `capi-exports-*` / `capi-class-from-name-*.log`；检查是否误装双探针；不回退 Win interop。

---

### Task 3 — 改 `install-loader.sh`：部署 Host；禁止默认部署 capi-probe

**执行方：** demo建造-CAPI

**Files:**
- Modify: `loader/scripts/install-loader.sh`
- Optional: `loader/scripts/install-host-check.sh` 或内联检查
- Modify: `docs/loader-host-gate.md`（文件列表检查步骤）

**Interfaces:**
- Consumes: Task 1 Release DLL 路径
- Produces: 安装后 `$GAME/BepInEx/plugins/` 含 Host DLL、**不含** capi-probe DLL

- [ ] **3.1** 在既有软链 / delayed Execute 之后增加：若 Host Release DLL 存在则 `cp -f` 到 `$GAME/BepInEx/plugins/`；若缺失则 **非零退出** 或明确警告（门禁要求可测：推荐 **缺产物则 exit 1**，避免「装了 Loader 却无 Host」静默）。
- [ ] **3.2** 明确**不**拷贝 `plugins/capi-probe/bin/**/*.dll`；可选：若目标 plugins 目录已有 `TpmMacCapiProbe.dll`，打印警告建议移除（避免双 GUID）。
- [ ] **3.3** 安装结束打印文件列表检查提示：`ls BepInEx/plugins/` 应见 Host、不应见 capi-probe。
- [ ] **3.4** 跑一遍 `./loader/scripts/install-loader.sh` → 冷启动 → 复验场景①（脚本部署路径）；更新 evidence / 门禁勾选场景①。

**完成判据：** 干净安装后 plugins 含 Host、不含 capi-probe；场景①经脚本路径通过。  
**失败回退：** 修脚本路径/AssemblyName；门禁检查文件列表失败则本规格不通过。

**示意片段（实现时按实际 DLL 名微调）：**

```sh
HOST_DLL="$REPO_ROOT/plugins/mac-il2cpp-host/bin/Release/TpmMacIl2CppHost.dll"
PLUGINS="$GAME/BepInEx/plugins"
mkdir -p "$PLUGINS"
if [ ! -f "$HOST_DLL" ]; then
  echo "Missing Host build: $HOST_DLL (build plugins/mac-il2cpp-host Release first)" 1>&2
  exit 1
fi
cp -f "$HOST_DLL" "$PLUGINS/"
# Do NOT install capi-probe
if [ -f "$PLUGINS/TpmMacCapiProbe.dll" ]; then
  echo "WARN: TpmMacCapiProbe.dll present; remove to avoid dual GUID load" 1>&2
fi
echo "Host deployed: $PLUGINS/TpmMacIl2CppHost.dll"
```

---

### Task 4 — Multiselect：`BepInDependency(Host)` + Ready/smoke 日志（无玩法）

**执行方：** demo建造（主改 multiselect）；demo建造-CAPI 可协助联调门禁场景②

**Files:**
- Modify: `plugins/multiselect/Plugin.cs`
- Modify: `plugins/multiselect/TpmMacMultiselect.csproj`（ProjectReference → mac-il2cpp-host，或等价引用）
- Create: `docs/evidence/loader-host-multiselect-smoke-x64.log`
- Modify: `docs/loader-host-gate.md`（场景②）

**Interfaces:**
- Consumes: `HostIds.Guid`；`HostFacade.Ready`（或 `IIl2CppHost`）
- Produces: 依赖链日志；场景②勾选素材  
- **禁止 Produce：** Shift / Selection / BuildGesture 玩法行为

- [ ] **4.1** csproj 增加对 `mac-il2cpp-host` 的 `ProjectReference`（或 HintPath 到其 Release DLL）；确保能编译引用 `HostIds` / `HostFacade`。
- [ ] **4.2** `Plugin.cs` 增加：  
  `[BepInDependency(TpmMacIl2CppHost.HostIds.Guid, BepInDependency.DependencyFlags.HardDependency)]`
- [ ] **4.3** `Load`：`LogInfo` 已加载；若 `HostFacade.Ready` → `LogInfo "[MS] host ready / smoke ok"`；若未 Ready → `LogError "[MS] host not ready; refusing smoke success"` 并 return（HardDependency 回退）。
- [ ] **4.4** **禁止**修改 `Selection/`、`Input/ShiftInput.cs`、`BuildActions/*` 的玩法接线；本任务 diff 应仅限 Plugin/csproj（及必要 using）。
- [ ] **4.5** 部署 Host + Multiselect → 冷启动 → 摘录日志：两者均加载 + host ready/smoke ok → 门禁场景②。

**完成判据：** 日志证明依赖满足后的成功路径；源码无玩法接线增量。  
**失败回退：** HardDependency 不生效 → 插件内显式探测 + Error（规格 §6.5）；仍须满足「不静默生效」。

---

### Task 5 — 缺 Host 负例门禁

**执行方：** demo建造-CAPI（主）；demo建造 协助确认 Multiselect 失败信号

**Files:**
- Create: `docs/evidence/loader-host-missing-host-negative-x64.log`
- Modify: `docs/loader-host-gate.md`（场景③）

**Interfaces:**
- Consumes: Task 4 的 HardDependency / 自检逻辑
- Produces: 可观测失败信号摘录；场景③勾选

- [ ] **5.1** 从 `$GAME/BepInEx/plugins/` **移除** Host DLL（保留 Multiselect DLL）；确认无 `TpmMacIl2CppHost.dll`。
- [ ] **5.2** 冷启动 `./run_bepinex.sh`；采集 `LogOutput.log`。
- [ ] **5.3** 验收：Multiselect **不得**出现「host ready / smoke ok」成功路径；必须出现其一——Chainloader 依赖缺失 / 插件未加载 / Multiselect `LogError` 拒绝。将关键词摘录入 evidence。
- [ ] **5.4** 恢复 Host DLL；门禁勾选场景③；注明采用的失败信号类型（依赖机制 vs 自检）。

**完成判据：** 负例 evidence + 门禁场景③勾选；成功标准 3 满足。  
**失败回退：** 若仍「静默像正常」→ 强制 Task 4.3 自检 Error 路径并重跑负例；不得放宽标准。

---

### Task 6 — README 两章 + 门禁收束 + 成功标准 1–6 自检

**执行方：** demo建造-CAPI（门禁/README Host 章）；demo建造 可补 README 多选冒烟句（无玩法）

**Files:**
- Modify: `README.md`（① 只装 Loader **含 Host**；② 再装多选并声明依赖 Host）
- Modify: `docs/loader-host-gate.md`（成功标准映射表勾选）
- Optional mirror: `/workspace/demo-lab/plans|specs/` 回写说明一行

**Interfaces:**
- Consumes: Task 0–5 全部 evidence
- Produces: 文档两章可复现；规格 §4 映射全部可勾

- [ ] **6.1** README ①：安装脚本会部署 Host；钉死版本 / 路径 / `run_bepinex.sh` x64；预期日志含 Host；维持 interop 配置说明。
- [ ] **6.2** README ②：构建并拷贝 Multiselect；硬依赖 Host GUID `com.tpm.mac.il2cpp-host`；**写明本阶段无 Shift 玩法**；缺 Host 会失败而非静默。
- [ ] **6.3** 门禁填写成功标准映射（见下表）并签名日期（Asia/Shanghai）。
- [ ] **6.4** 确认 `capi-probe` 未进默认安装说明；轨 A 不停句仍在。

**完成判据：** README 两章完整；`loader-host-gate.md` 三场景全勾 + 标准 1–6 自检全勾。  
**失败回退：** 缺哪条补哪条 evidence；不拿玩法 demo 冒充本切片。

---

## 完成判据（整计划）

| # | 判据 | 对应任务 |
|---|------|----------|
| 1 | 仅 Loader 安装（含 Host）冷启动 → Host 加载 + 非空 class 日志 | T2, T3 |
| 2 | 再部署 Multiselect → 加载 + host ready/smoke | T4 |
| 3 | 缺 Host → Multiselect 不静默成功 | T5 |
| 4 | `loader/` / `mac-il2cpp-host/` / `multiselect/` 可分构建；Host 源码不在 loader 树 | T1, T3 |
| 5 | README 两章 + `loader-host-gate.md` + evidence | T0, T6 |
| 6 | 无玩法恢复；无 Win 直拷；无 CrossOver；不停轨 A；capi-probe 非默认安装 | T0, T4.4, T6 |

---

## 风险与回退

| 触发 | 回退 |
|------|------|
| Host `dlsym` / `ClassFromName` 回归失败 | 停本规格通过；对照 `docs/evidence/capi-*` 复现；不直拷 Win interop |
| `BepInDependency` 在 IL2CPP 链不生效 | Multiselect 内显式探测 `HostFacade` / 程序集 + `LogError`；重跑 Task 5 |
| 安装脚本漏拷 Host | 修脚本；门禁查 `plugins/` 文件列表；缺 DLL 应 fail 安装 |
| `capi-probe` 与 Host 双加载 | 脚本警告 + README 指示删除探针 DLL；门禁场景①文件列表 |
| 游戏 buildid / Unity 变更 | 重跑 Host 冒烟与三场景门禁；更新基线表 |
| 轨 A 阴性或改仓库 | **忽略**对门禁的影响；约定不改 Host/Multiselect GUID 与 install 脚本职责边界 |

---

## 成功标准映射（规格 §4 → 任务）

| 规格 §4 | 计划任务 | 门禁/证据 |
|---------|----------|-----------|
| 1 Loader+Host 单独可用 | Task 2, 3 | `loader-host-gate.md` 场景①；`docs/evidence/loader-host-host-smoke-x64.log` |
| 2 多选冒烟依赖链 | Task 4 | 场景②；`loader-host-multiselect-smoke-x64.log` |
| 3 缺 Host 不静默生效 | Task 5 | 场景③；`loader-host-missing-host-negative-x64.log` |
| 4 工程拆分 | Task 1, 3 | 目录边界；install 只拷产物 |
| 5 文档两章 | Task 6 | `README.md` ①②；门禁复现步骤 |
| 6 约束遵守 | Task 0, 4.4, 6 | 门禁「不停轨 A / 玩法停 / 禁 Win 直拷 / 禁 capi-probe 默认装」勾选 |

---

## 执行分工（再钉一次）

| 任务 | demo建造-CAPI | demo建造 |
|------|---------------|----------|
| Task 0 基线/门禁框 | **主** | 自检只读 |
| Task 1 Host 晋升 | **主** | — |
| Task 2 Host 冒烟 | **主** | — |
| Task 3 install-loader.sh | **主** | — |
| Task 4 Multiselect 依赖冒烟 | 联调协助 | **主**（且继续轨 A） |
| Task 5 缺 Host 负例 | **主** | 协助确认失败信号 |
| Task 6 README + 收束 | **主** | 可补②章冒烟句 |

**并行：** demo建造 的轨 A 任务与本计划 Task 4 **互不阻塞**；本计划通过**不**要求轨 A 通过。

---

## 待澄清（有默认，不阻塞开工）

| 项 | 默认决策 | 若用户改口 |
|----|----------|------------|
| Host 程序集名 | `TpmMacIl2CppHost` | 改 csproj + install 路径 |
| Multiselect 引用方式 | ProjectReference | 改为仅 GUID + 反射自检 |
| 安装缺 Host 产物 | `exit 1` | 改为警告（不推荐；削弱标准 1） |
| 冒烟类型 | `System.String` | 其它稳定 corlib/Unity 类型 |
| `capi-probe` 目录 | 保留遗物 | 另开修订再删 |

无开放 TBD 阻塞 Task 0 开工。

---

## Self-review（计划作者已做）

1. **规格覆盖：** §4 六条 → 映射表；§5 非目标 → Global Constraints + Task 4.4；§6 模块/API/安装/工具/回退 → Architecture + Tasks 1–5；§8 交付物 → Tasks 产出。  
2. **无占位：** 无 TBD/TODO 步骤；类型与 GUID 已钉死；安装示意含具体路径。  
3. **类型一致：** `HostIds.Guid` / `IIl2CppHost` / `HostFacade.Ready` / `ClassFromName(imageHint, ns, name)` 在 Task 1→4 共用；与轨 B 三参数 `class_from_name` 对齐（非过时两参数草图）。
