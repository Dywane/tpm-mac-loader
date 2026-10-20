# Spec: Two Point Museum — Mac Mod Loader 宿主完善（C API Host + 多选冒烟接入）

- **状态:** 已批准（2026-10-19 Asia/Shanghai）；**修订 A4 指针**（2026-10-20）：稳定 `IRuntimeBridge` + 可换内核 — 见 `tpm-mac-plugin-runtime-multiselect-spec.md`（已批准）
- **日期:** 2026-10-19（Asia/Shanghai）
- **主题 slug:** `tpm-mac-loader-host`
- **用户选定切片:** host-plus-smoke（宿主 + 多选声明依赖并冒烟加载；**不开**玩法）
- **方案:** ① 平台插件随 Loader 交付（用户已 OK）
- **代码与文档落点（权威）:** `/Users/weidu/Documents/Project/TPM-muti-select`（`docs/specs|plans|research` + 源码）
- **协作镜像（可选）:** `/workspace/demo-lab/specs/tpm-mac-loader-host-spec.md`
- **前因:**
  - 多选两件套规格：`docs/specs/tpm-mac-multiselect-spec.md`（玩法 Task 仍停）
  - 调用桥规格：`docs/specs/tpm-mac-il2cpp-interop-spec.md` **修订 A3**；轨 B 已通过（门禁 `通过轨=B`，`il2cpp_class_from_name` → `System.String`）
- **与轨 A 关系:** 轨 A（Mac reg → Il2CppInterop）**继续并行收窄**，本规格不依赖、不阻断轨 A。

---

## 1. 背景

### 1.1 已通 / 未通

| 侧 | 状态 | 证据 |
|----|------|------|
| Mac Museum Mod Loader（BepInEx 钉死包 + delayed Execute） | **已通** | `loader/`、`docs/task1-loader-smoke.md` |
| 轨 B C API 探针 | **已通** | `plugins/capi-probe/`、`docs/evidence/capi-*`、`docs/interop-runtime-gate.md` 通过轨=B |
| 轨 A reg / Il2CppInterop | **进行中** | TaskA2+ 收窄；非本规格门槛 |
| Multi-Select 玩法 | **停** | 多选规格 Task 5–9；本切片仍不恢复 |
| Multi-Select 脚手架 | **有、未接宿主** | `plugins/multiselect/` 可加载，但无 `BepInDependency`、无 Host 契约 |

### 1.2 用户指示（2026-10-19）

「下一步做成一个 mod loader，完善一下，准备多选插件的接入。」  
验收切片：**host-plus-smoke** — Loader 附带 C API 宿主插件 + 依赖契约；多选只声明依赖并冒烟加载。

命题：**把已证明的 C API 桥产品化成随 Loader 交付的宿主，并让多选插件能正确依赖、加载、可观测失败。**

---

## 2. 目标用户

| 阶段 | 用户 | 说明 |
|------|------|------|
| 本切片 | 用户本人（Mac Steam 原生 TPM） | 本机装 Loader（含 Host）+ 可选装多选冒烟插件 |
| 紧接后续 | 自己（玩法实现） | **本规格通过后**再开多选玩法规格/计划恢复 |
| 更后 | 其他 Mac 玩家 / 工坊 | **不在本规格验收范围** |

---

## 3. 要证明什么（Demo 命题）

交付 **可安装的 Mac Museum Mod Loader 产品形态**，内含：

1. **既有注入宿主**（Doorstop + BepInEx 钉死包 + Mac 布局软链 + delayed Execute）——不重做，只完善安装路径以部署 Host。
2. **Mac IL2CPP C API 平台插件（Host）**——由 `capi-probe` 晋升；稳定 GUID；对外最小可调用面。
3. **Multi-Select 冒烟接入**——独立插件；硬依赖 Host；加载链可在日志验证；**不含** Shift 拾取/旋转/删除。

对齐 SeanB「Loader 与插件可拆」；Mac 侧因无 winhttp / 无可用静态 interop，**把 C API Host 打进 Loader 默认交付物**（方案 ①）。

---

## 4. 成功标准（可验收）

在用户本机原生 Mac Steam 安装上：

1. **Loader+Host 单独可用：** 仅执行 Loader 安装（含 Host 插件）后，用约定启动方式冷启动；`LogOutput.log` 出现 BepInEx 初始化、Host 插件加载、且至少一次 `il2cpp_class_from_name`（或 Host 封装等价）非空日志（允许 `System.String` / corlib 稳定类型）。
2. **多选冒烟依赖链：** 再部署 Multiselect 插件后，日志显示 Multiselect 已加载，且能观测到「Host 就绪」类信息（或等价：依赖满足后的成功路径日志）。
3. **缺 Host 不静默生效：** 故意不部署 Host（或卸载 Host DLL）时，Multiselect **不得**表现为正常可用；须有依赖失败 / 硬错误 / 明确拒绝加载的可观测信号（BepInEx 依赖机制或插件自检日志，二者择一并写入门禁）。
4. **工程拆分：** `loader/`、`plugins/mac-il2cpp-host/`、`plugins/multiselect/` 可分开构建；Host 源码不进 `loader/` 二进制树，仅由安装脚本从构建产物部署。
5. **文档：** README 或安装说明至少两章——① 只装 Loader（含 Host）；② 再装多选并声明依赖 Host/Loader；钉死版本、路径、启动方式可复现。
6. **约束遵守：** 不恢复多选玩法；不对抗 Denuvo；不换 CrossOver；不把 Win interop 直拷 Mac；不因本规格停轨 A。

**明确不算成功：** 仅 BepInEx 启动无 Host；仅 `capi-probe` 仍当正式交付；Multiselect 无依赖声明却「看起来加载了」；实现 Shift 玩法冒充本切片完成。

---

## 5. 非目标

- Shift 多选拾取 / 旋转 / 删除及任何玩法对标（仍属 `tpm-mac-multiselect` 玩法任务）。
- 工坊上架、一键安装给他人、第三方插件公共 SDK 文档站。
- 轨 A：Cpp2IL / Il2CppInterop 生成与托管 DX（并行继续，但非本规格门槛）。
- 扩满 C API（`runtime_invoke`、字段读写、泛型等）——本切片 **最小面** 即可；可留扩展点，不强制。
- 把 Host 逻辑打进 `BepInEx.Unity.IL2CPP.dll` 或 Doorstop（禁止方案 ③）。
- 三件套分装成三个独立 Workshop 项（方案 ②；本切片不采纳）。
- CrossOver / Proton / winhttp.dll 路径。
- 绕过或削弱 Denuvo。

---

## 6. 方案（主路径）

### 6.1 模块边界

| 模块 | 路径 | 职责 | 不职责 |
|------|------|------|--------|
| **Loader 包装** | `loader/` | 钉死 BepInEx 包、安装/卸载脚本、Mac 软链、delayed Execute、**部署 Host 构建产物** | 不含多选玩法；不含 Host 源码主体 |
| **C API Host** | `plugins/mac-il2cpp-host/`（新建；自 `capi-probe` 晋升） | 稳定 GUID；`dlsym`/`il2cpp_*`；`TryResolve` + `ClassFromName`（及探针已验证的最小封装）；就绪状态可供依赖方查询 | 不做玩法；不做 Il2CppInterop 生成 |
| **Multi-Select（冒烟）** | `plugins/multiselect/` | `BepInDependency(Host GUID)`；Load 冒烟日志；可选极薄「Host 就绪」检查 | 本切片不接 `IItemSelectionBridge` 玩法 |
| **capi-probe** | `plugins/capi-probe/` | 证据遗物：保留源码与 `docs/evidence/capi-*`；**不**再作为默认安装物 | 不随 Loader 安装脚本部署 |

**建议 Host GUID（计划阶段可微调，须钉死进门禁）：** `com.tpm.mac.il2cpp-host`  
**既有 Multiselect GUID：** `com.tpm.mac.multiselect`（保持）  
**退役探针 GUID：** `com.tpm.mac.capi-probe`（不再默认加载）

### 6.2 对外最小 API（Host）

本切片必须具备、可被 Multiselect（及日后玩法）引用的面：

- `TryResolve` / 就绪标志（`Ready`）——进程内符号绑定成功与否。
- `ClassFromName`（或等价封装）——按 image/namespace/name 解析，非空指针可打日志。

**允许**保留探针阶段已绑定的辅助符号（`GetCorlib`、`ClassGetName` 等）但不扩验收面。  
**不要求**本切片交付 `runtime_invoke` 或选择/建造专用桥。

Multiselect 冒烟路径：依赖 Host → Load → 日志确认 Host `Ready`（或依赖系统已保证加载顺序后打「smoke ok」）。

### 6.3 安装与加载链

```
install-loader.sh
  → Doorstop + BepInEx + softlinks + delayed Execute
  → 拷贝 mac-il2cpp-host.dll → BepInEx/plugins/
（可选）部署 multiselect.dll → BepInEx/plugins/
run_bepinex.sh（x86_64）
  → BepInEx → Host Load →（若存在）Multiselect Load
```

配置维持：`UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false`（轨 A 产物就位前；Host 不依赖自动生成）。

### 6.4 工具钉死（继承）

| 项 | 约束 |
|----|------|
| Loader 包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286`（`loader/VERSION.txt`） |
| 启动架构 | 史实 **x64 Rosetta**（与 Task1 / 轨 B 一致）；若改 arm64 须另开修订并重跑门禁 |
| Unity / build | Unity `6000.0.63f1`；Steam buildid `25799969`（失效则重跑 Host 冒烟） |
| 游戏 | 原生 Mac Steam AppID `2185060` |

### 6.5 失败回退（本规格内）

| 触发 | 回退 |
|------|------|
| Host `dlsym` / `class_from_name` 回归失败 | 停本规格通过；对照轨 B evidence 复现；不回退到 Win interop 直拷 |
| Multiselect 依赖属性不生效 | 改为插件内显式探测 Host 程序集/就绪标志 + Error 日志；仍须满足「不静默生效」 |
| 安装脚本漏拷 Host | 修脚本；门禁检查 `plugins/` 文件列表 |
| 轨 A 阴性 | **忽略**；不阻断本规格 |

---

## 7. 风险与假设

| 项 | 说明 |
|----|------|
| BepInDependency 与 IL2CPP 插件加载顺序 | **部分验证依赖实测**；门禁必须用缺 Host 负例证明 |
| Host 对外 API 稳定性 | 本切片钉最小面；日后扩 API 须版本/修订，避免默默破多选编译 |
| `capi-probe` 与 Host 同机残留 | 双 GUID 可能双加载；安装说明与脚本须避免同时部署探针 |
| 游戏小版本更新 | buildid / Unity 变化可能导致符号或启动差异；门禁钉版本 |
| 轨 A 并行改仓库 | 约定 Host/Multiselect/Loader 安装脚本边界；轨 A 继续用 `interop-probe/`，互不改对方 GUID |

---

## 8. 交付物（本规格通过时）

1. 本文件（批准后）位于 `docs/specs/tpm-mac-loader-host-spec.md`。
2. `plugins/mac-il2cpp-host/` 源码 + 构建产物部署路径约定。
3. 更新后的 `loader/scripts/install-loader.sh`（或等价）：安装时部署 Host。
4. `plugins/multiselect/`：`BepInDependency` + 冒烟日志（无玩法接线）。
5. 门禁笔记（新建 `docs/loader-host-gate.md` 或扩写既有门禁一节）+ 必要 `docs/evidence/` 日志摘录（Loader+Host；Host+Multiselect；缺 Host 负例）。
6. README / 安装说明两章更新。
7. （可选）`/workspace/demo-lab/specs/` 同名镜像。

**不交付：** 多选玩法、工坊包、轨 A 完成、完整 C API SDK。

---

## 9. 批准后下游

1. **计划架构** 写 `docs/plans/tpm-mac-loader-host-plan.md`（可验证小任务 + 门禁）。
2. **demo建造-CAPI** 为主执行方（Host 晋升、安装脚本部署、门禁）；**demo建造** 可协助 Multiselect 依赖冒烟（若需），**不得**借机恢复玩法；轨 A 任务仍归 demo建造且互不停工。
3. 本规格通过后，再开多选玩法规格修订或恢复 `tpm-mac-multiselect` 计划中的玩法任务。
4. 规格未批准前：建造方待命，不按本设计改默认安装物。

---

## 10. 修订记录

### 初版 — 2026-10-19（Asia/Shanghai）

- **触发：** 轨 B A3 探针已通过；用户要求「做成 mod loader，完善，准备多选插件接入」；验收选 **host-plus-smoke**；方案选 **① 平台插件随 Loader 交付**。
- **本修订做什么：** 产品化 C API Host 进 Loader 交付；Multiselect 依赖 + 冒烟；文档与门禁。
- **本修订不做什么：** 玩法；方案 ②/③；扩大 C API 验收面；停轨 A。

### 修订 A4 指针 — 2026-10-20（Asia/Shanghai）

- Host 从「C API 冒烟」升级为稳定运行时门面 + 可换内核；玩法恢复。执行规格：`tpm-mac-plugin-runtime-multiselect-spec.md`。
