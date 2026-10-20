# Loader Host 门禁（tpm-mac-loader-host）

> **切片：** host-plus-smoke（宿主 + 多选声明依赖冒烟；**不开**玩法）  
> **计划：** `docs/plans/tpm-mac-loader-host-plan.md`  
> **规格：** `docs/specs/tpm-mac-loader-host-spec.md`  
> **门禁建框：** Task 0（2026-10-19 Asia/Shanghai）  
> **正式 Host GUID：** `com.tpm.mac.il2cpp-host`（须在下方核对栏手填并勾选）

---

## 0. 基线与声明（Task 0）

### 0.1 基线表

| 项 | 值 | 核对来源 |
|----|-----|----------|
| Loader 包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286` | `loader/VERSION.txt` 原文 `package=` 行 |
| 游戏默认路径 | `$HOME/Library/Application Support/Steam/steamapps/common/Two Point Museum` | Steam Mac 布局 / Task1 |
| Unity | `6000.0.63f1` | Task1 LogOutput / 轨 B 门禁 |
| Steam buildid | `25799969` | 规格钉死；变则重跑 Host 冒烟 |
| AppID | `2185060` | Two Point Museum |
| 启动架构 | **x64 Rosetta**（`arch -x86_64`） | 与 Task1 / 轨 B 一致 |
| Interop 配置 | `UpdateInteropAssemblies=false`（及 `PreloadIL2CPPInteropAssemblies=false`） | Host **不**依赖自动生成 |

**`loader/VERSION.txt` 原文摘录（Task 0 核对）：**

```
package=BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286
url=https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789%2B6cea286.zip
sha256=cc2341544d271f8bcea4e7415acad10766e968d236aaee30cdd79780ec7db00b
doorstop=4.5.0
pinned_for=tpm-mac-multiselect Task 1
```

### 0.2 轨 B 链接（前因已通）

| 项 | 状态 / 指针 |
|----|-------------|
| `docs/interop-runtime-gate.md` | **通过轨 = B**（A3 通过框，2026-10-19 Asia/Shanghai） |
| `docs/evidence/capi-class-from-name-x64.log` | `[CAPI] class_from_name ok ns=System name=String ptr=0x10D9E5390 got=System.String`（非空） |
| 等价补充 | `docs/evidence/capi-exports-dlsym-x64.md`：`il2cpp_class_from_name -> 0x130CDABD1`；wrappers ready |

本切片**不重做** `dlsym` / `class_from_name`；只做晋升 + 安装链 + 依赖契约。

### 0.3 废止声明：`capi-probe` 不再默认交付

- **废止：** `plugins/capi-probe/` / GUID `com.tpm.mac.capi-probe` **不再**作为 Loader **默认安装物**。
- **正式交付 GUID：** `com.tpm.mac.il2cpp-host`（工程落点 `plugins/mac-il2cpp-host/`，随 Loader 安装脚本部署构建产物）。
- **遗物：** `capi-probe` 目录与 `docs/evidence/capi-*` **保留作证据**；安装脚本**禁止**默认拷贝其 DLL。
- 同机若残留双 GUID，有双加载风险；脚本与文档须避免同时部署。

### 0.4 不停轨 A / 玩法保持停

- **轨 A**（`plugins/interop-probe/`，主题 `tpm-mac-il2cpp-interop`）**继续并行**；本计划**不阻断、不改其 GUID/职责**。
- **多选玩法 Task 5–9 保持停**；本切片**禁止** Shift 拾取/旋转/删除或接线 `IItemSelectionBridge` 玩法路径。
- **禁：** Win interop 直拷 Mac；方案②/③；扩满 C API（非本切片验收）。

---

## Host GUID 核对栏

| 项 | 手填 / 勾选 |
|----|-------------|
| 期望 Host GUID | `com.tpm.mac.il2cpp-host` |
| 实测 / 日志中的 GUID | `com.tpm.mac.il2cpp-host`（LogOutput：`GUID=com.tpm.mac.il2cpp-host`） |
| 与期望一致 | ☑ |

（Task 2 冒烟已手填；见 `docs/evidence/loader-host-host-smoke-x64.log`。）

---

## 三场景门禁（Task 2–5 已填；Task 6 收束保持勾选）

### 场景① Loader + Host 单独可用

- 状态：☐ 未跑 / ☑ 通过 / ☐ 失败
- 证据：`docs/evidence/loader-host-host-smoke-script-x64.log`（脚本部署路径；手工基线见 `loader-host-host-smoke-x64.log`）
- 摘要：**脚本部署通过**（Task 3：`./loader/scripts/install-loader.sh` 在移除 Host 后 `cp -f` 恢复 `TpmMacIl2CppHost.dll`；**不**拷贝 capi-probe）。冷启动 `arch -x86_64 ./run_bepinex.sh`；x64；Host GUID `com.tpm.mac.il2cpp-host` 加载；`[HOST] Ready`；`[HOST] class_from_name ok ns=System name=String ptr=0x10D865390 got=System.String`。安装后 plugins：`TpmMacIl2CppHost.dll` + InteropProbe + Multiselect；**无** `TpmMacCapiProbe.dll`。缺 Host 产物时脚本 `exit 1`（已测）。

### 场景② Host + Multiselect 依赖链

- 状态：☑ 通过（Task 4，2026-10-19 17:49 Asia/Shanghai）
- 证据：`docs/evidence/loader-host-multiselect-smoke-x64.log`
- 摘要：冷启动 x64：Host GUID=`com.tpm.mac.il2cpp-host` → `[HOST] Ready; HostFacade set` → Multiselect Load → `[MS] host ready / smoke ok`。Multiselect 引用 `HostIds.Guid` HardDependency + `HostFacade.Ready`；无玩法接线。

### 场景③ 缺 Host 负例（不静默生效）

- 状态：☑ 通过（Task 5，2026-10-19 17:56 Asia/Shanghai）
- 证据：`docs/evidence/loader-host-missing-host-negative-x64.log`
- 摘要：从 `BepInEx/plugins/` 移除 `TpmMacIl2CppHost.dll`（备份于 `BepInEx/.task5-host-aside-outside-plugins/`，非 plugins 子目录）；保留 Multiselect + InteropProbe。冷启动 `arch -x86_64 ./run_bepinex.sh`。Chainloader：**Could not load [TPM Mac Multi-Select 0.1.0] because it has missing dependencies: com.tpm.mac.il2cpp-host**；**无** `[MS] host ready / smoke ok`；Multiselect 未进入 Load/自检。测后已恢复 Host DLL。
- 失败信号形态（择一记入）：☑ Chainloader 依赖失败 / ☐ Multiselect 显式 Error 后 return
  - **类型：依赖机制**（`BepInDependency(HostIds.Guid, HardDependency)` → Chainloader 拒载）；自检 `LogError("[MS] host not ready…")` 路径未触发（插件未加载）。

---

## 成功标准自检（标准 1–6；Task 6 收束）

> **签名：** Task 6 收束 — 2026-10-19 17:57 Asia/Shanghai（demo建造-CAPI）  
> **规格：** `docs/specs/tpm-mac-loader-host-spec.md` §4（本文不改规格原文，仅映射勾选）  
> **三场景：** ①②③ 均已勾选（见上）

| # | 规格 §4 摘要（勿改原文） | 门禁/证据 | 勾选 |
|---|--------------------------|-----------|------|
| 1 | Loader+Host 单独可用 | 场景①；`docs/evidence/loader-host-host-smoke-x64.log` + `loader-host-host-smoke-script-x64.log` | ☑ |
| 2 | Host+Multiselect 依赖链可观测 | 场景②；`docs/evidence/loader-host-multiselect-smoke-x64.log` | ☑ |
| 3 | 缺 Host 不静默生效 | 场景③；`docs/evidence/loader-host-missing-host-negative-x64.log`（Chainloader missing dep） | ☑ |
| 4 | Host GUID = `com.tpm.mac.il2cpp-host` 钉死 | 上文「Host GUID 核对栏」☑；工程 `plugins/mac-il2cpp-host/HostIds.cs` | ☑ |
| 5 | 安装链部署 Host、**不**默认装 `capi-probe` | Task 3 `install-loader.sh`；场景①文件列表；README ①；§0.3 废止声明 | ☑ |
| 6 | 不停轨 A；玩法停；禁 Win 直拷 | §0.4 声明；README 轨 A / 「本阶段无 Shift」；无 Win interop 直拷进 Host | ☑ |

### 规格 §4 → 计划任务映射（收束核对）

| 规格 §4 | 计划任务 | 门禁/证据 |
|---------|----------|-----------|
| 1 Loader+Host 单独可用 | Task 2, 3 | 场景①；host-smoke（手工+脚本） |
| 2 多选冒烟依赖链 | Task 4 | 场景②；multiselect-smoke |
| 3 缺 Host 不静默生效 | Task 5 | 场景③；missing-host-negative |
| 4 工程拆分 | Task 1, 3 | `loader/` / `mac-il2cpp-host/` / `multiselect/` 分树；install 只拷产物 |
| 5 文档两章 | Task 6 | `README.md` ①②；本门禁复现步骤 |
| 6 约束遵守 | Task 0, 4.4, 6 | §0.3–0.4；标准 6 勾选；README 无「capi-probe 默认安装」 |

### 6.4 文档自检

| 检查项 | 结果 |
|--------|------|
| 文档无「capi-probe 默认安装」措辞 | ☑（§0.3 废止；README 标明遗物、脚本不拷） |
| 轨 A 不停句仍在 | ☑（§0.4；README 轨 A 注） |
| 规格 §4 原文未被改写 | ☑（仅本门禁映射勾选） |

---

## 变更记录

- 2026-10-19 17:57 Asia/Shanghai：Task 6——README ①② 更新；成功标准 1–6 全勾；规格 §4 映射表签名；三场景保持勾选；确认无 capi-probe 默认安装、轨 A 不停。

- 2026-10-19 Asia/Shanghai：Task 5——缺 Host 负例通过；失败信号=**依赖机制**（Chainloader missing dep `com.tpm.mac.il2cpp-host`）；evidence `loader-host-missing-host-negative-x64.log`；Host 已恢复；场景③勾选。

- 2026-10-19 Asia/Shanghai：Task 3——`install-loader.sh` 部署 Host、禁默认 capi-probe；缺产物 exit 1；场景① **脚本部署通过**；evidence `loader-host-host-smoke-script-x64.log`。场景②已由 Task 4 勾选（`loader-host-multiselect-smoke-x64.log`）。

- 2026-10-19 Asia/Shanghai：Task 2——场景① **手工部署通过**；Host GUID 核对栏勾选；evidence `loader-host-host-smoke-x64.log`（`got=System.String` ptr=`0x10D5A5390`）。
- 2026-10-19 Asia/Shanghai：Task 4 — Multiselect `BepInDependency(HostIds.Guid)` + `HostFacade.Ready` 冒烟通过；场景②勾选；evidence `loader-host-multiselect-smoke-x64.log`。

- 2026-10-19 Asia/Shanghai：Task 0 建框——基线表、轨 B 链接、废止 `capi-probe` 默认交付、轨 A 不停 / 玩法停声明、三场景空栏 + Host GUID 核对栏。
