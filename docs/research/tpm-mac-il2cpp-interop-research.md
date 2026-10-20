# TPM Mac — IL2CPP interop / 等价调用调研报告

**调研日期：** 2026-10-19（Asia/Shanghai）  
**范围：** 只做公开信息调研；目标是「证明 Mac 原生进程内托管或等价调用可用」，**不是**多选玩法实现。  
**非目标：** 不碰 Denuvo / 不对抗反篡改、不做 CrossOver、不做多选玩法。  
**本地硬事实来源：** 用户仓库 `/Users/weidu/Documents/Project/TPM-muti-select` 的 `docs/task4-interop-gate.md`、`docs/evidence/task4-cpp2il-pre21-*.log`、`docs/game-hooks.md`（2026-10-19）。

**标注约定**

| 标记 | 含义 |
|------|------|
| **证据** | 有公开 URL / 本地门禁日志可核 |
| **未验证** | 合理推断或社区讨论，尚未在 TPM Mac 上复现 |

---

## 0. 本地硬事实（门禁失败摘要）

| 项 | 值 |
|----|-----|
| 游戏 | Two Point Museum Steam Mac，AppID `2185060` |
| Unity | `6000.0.63f1` |
| metadata | `global-metadata` **v31**（Cpp2IL 记为 31.1） |
| GameAssembly | Mach-O **universal**（x64 + arm64） |
| Cpp2IL | `2022.1.0-pre-release.21` |
| x64 thin | `codereg: 0x34CC0D0`（落在 `__bss`，filesize=0），`metareg: 0x0` → 失败 |
| arm64 thin | `codereg: 0x0`，`metareg: 0x0` → 失败 |
| BepInEx A 侧 | Loader + delayed Execute **已通**；插件能加载；`UpdateInteropAssemblies=false` |
| B 侧 | 无 interop 程序集 → 无法做托管类型桥 |

证据文件：`docs/task4-interop-gate.md`；`docs/evidence/task4-cpp2il-pre21-x64.log` / `task4-cpp2il-pre21-arm64.log`。

Cpp2IL 日志要点（**证据**）：metadata 侧 30970 types 初始化成功；Mach-O 标 *experimental*；MetadataReg 按 type-count `0x78FA` 字节搜索 **0 hits**（与 BSS 无磁盘初值一致）；CodeReg 经 mscorlib 路径在 x64 推到 BSS VA。

---

## 1. 「通常开发 mod 是怎么做的？」——Unity IL2CPP / TPM 生态

### 1.1 两条完全不同的「模组」轨道（证据）

1. **官方 Steam Workshop 内容模**（墙纸 / 装饰 / 物品资源）  
   - 走 Two Point Studio 的 **Modding SDK + Unity Editor**（Museum 工坊指南要求 Editor **2020.3.47f1**，与运行时 Unity 6 **不是同一条链**）。  
   - 链接：工坊讨论 / 安装指南 https://steamcommunity.com/workshop/discussions/18446744073709551615/552359478871263253/?appid=2185060 ；官方 FAQ https://community.twopointcounty.com/two-point-studios/two-point-museum/blogs/82-museum-community-faq  
   - **做不到** Shift 多选这类进程内代码行为。

2. **代码注入模**（SeanB QoL、TPM-Mod-Engine、BepInEx 插件）  
   - 需要把托管/原生代码打进游戏进程。对 IL2CPP 游戏，社区主流是 **BepInEx 6 Unity.IL2CPP**（或自研 winhttp 代理加载原生 DLL）。

### 1.2 Windows 上典型链路（TPM 现状）

对 **Two Point Museum (Windows)**，公开可见至少两套入口：

#### A. SeanB / Coding Dex：**winhttp.dll 代理 Loader**（证据）

- Workshop「Museum Mod Loader — Windows Only」`3817145646`：把 `winhttp.dll` + `Mods/` 拷到 `TPM.exe` 旁；通过 **Windows x64 WinHTTP proxy** 加载 native mods。  
  https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646  
- Nexus 同作者说明：https://www.nexusmods.com/twopointmuseum/mods/18  
- 血缘：Coding Dex `TPM-Mod-Engine` 自述 *「same technique (DLL Proxy) used by BepInEx, MelonLoader and UnityDoorstop」*，且 **不依赖** BepInEx。  
  https://github.com/coding-dex/TPM-Mod-Engine  

示意图：

```text
TPM.exe 启动
  → 加载旁路 winhttp.dll（代理）
    → 读 Mods/loader.ini
      → LoadLibrary 各 QoL native DLL（Multi-Select 等）
```

这条链 **不经过** Cpp2IL / Il2CppInterop；插件侧是原生/自研桥，细节无公开源码（**未验证**具体 hook 点）。

#### B. 通用 Unity IL2CPP：**BepInEx 6**（证据）

标准教学路径（BepInEx / Il2CppInterop 文档）：

```text
winhttp / Doorstop 注入
  → BepInEx Preloader + IL2CPP Chainloader
    → Cpp2IL：GameAssembly.dll + global-metadata.dat → Dummy DLL
      → Il2CppInterop.Generator → BepInEx/interop/*.dll
        → 插件引用 interop，Harmony / Il2Cpp 类型调用
```

- 安装文档：https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html  
- Il2CppInterop CLI：https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Command-Line-Usage.md  
- **注意：** 即便在 Windows，TPM 也有公开失败案例：BepInEx #1109（Unity `6000.0.37f1`，Cpp2IL **成功**找到 `codereg`/`metareg`，随后 Il2CppInterop **Pass16 XrefScan** 报 `Sequence contains no elements`）。  
  https://github.com/BepInEx/BepInEx/issues/1109 （创建 2025-05-18；访问 2026-10-19）  
  相关修复讨论 PR（**未合并 / 未验证是否修 TPM**）：https://github.com/BepInEx/Il2CppInterop/pull/262  

因此：「Windows 可以 mod」在 TPM 上更准确的说法是：**winhttp 原生 Loader 路径已被作者跑通**；**完整 BepInEx+Il2CppInterop 自动生成在 TPM 上仍可能翻车**（#1109）。

### 1.3 Steam Deck 为何能跟 Windows 走（证据）

Workshop「Museum Mod Loader — Steam Deck Only」`3819798898` 明确要求：

1. 强制 **Proton Experimental**（跑的是 **Windows `TPM.exe` + `GameAssembly.dll`**，不是 Linux/Mac 原生二进制）；  
2. 安装 **BepInEx-Unity.IL2CPP-win-x64**（作者给出 be.788 zip）；  
3. Launch Options：`WINEDLLOVERRIDES="winhttp=n,b" %command%`；  
4. 再叠 TPM Mod Loader。  

https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898  

这与 BepInEx 官方 Proton/Wine 说明一致：Unix 上跑 **Windows 游戏** 时，需让 Wine 优先加载游戏目录里的 `winhttp.dll` 代理。  
https://docs.bepinex.dev/articles/advanced/proton_wine.html  

**结论（证据）：** Deck「能 mod」= **Windows PE 工具链 + Proton DLL override**，**不是**原生 Linux IL2CPP / Mach-O 路径。与 Mac 原生问题正交。

---

## 2. 「为什么 Windows / Steam Deck 可以，Mac 不行？」——机制对照

| 维度 | Windows / Deck(Proton) | Mac 原生（本仓库硬事实） |
|------|------------------------|---------------------------|
| 游戏二进制 | PE：`TPM.exe` + `GameAssembly.dll` | Mach-O `.app` + **universal `GameAssembly.dylib`** |
| 注入入口 | `winhttp.dll` 旁路（Doorstop 同族）在 PE 下天然可用 | **无** winhttp PE 代理；需 macOS 专用 loader（dylib / `run_bepinex.sh` / 已验证的 BepInEx Mac 注入） |
| Cpp2IL 找 registration | Windows TPM：**能找到**非零 `codereg`/`metareg`（#1109 日志） | x64：`codereg` 落在 **`__bss`**（磁盘无初值），`metareg=0`；arm64：双 0（task4 证据） |
| Il2CppInterop 生成 | 仍可能在 XrefScan 失败（#1109）；但社区至少有 PE 上的分析起点 | 静态生成被 Cpp2IL 门禁拦住；自动 `UpdateInteropAssemblies` 同路径失败 |
| 运行时模块基址 | `Process.Modules` 可枚举 `GameAssembly.dll` | 历史上 `Process.Modules` **只见 player、不见 dylib**（Il2CppInterop #206，2025-03）；PR #272（2026-06）用 `dlopen`/`dladdr` 修基址（**合并进 Il2CppInterop，是否已进当前 BE 构建 → 未验证**） |
| BepInEx 官方兼容表 | Unity IL2CPP Windows ✔️ | Unity IL2CPP OSX 标 ❌（仓库 README 兼容表）；BE 仍提供 `Unity.IL2CPP-macos-x64` 实验包 |

关键链接：

- Il2CppInterop #206（Mac 模块枚举死路）：https://github.com/BepInEx/Il2CppInterop/issues/206  
- Il2CppInterop PR #272（dlopen 修 macOS）：https://github.com/BepInEx/Il2CppInterop/pull/272  
- BepInEx 兼容表：https://github.com/BepInEx/BepInEx  
- Cpp2IL Mach-O *experimental* + universal 支持（pre.20+）：https://github.com/SamboyCoding/Cpp2IL/releases ；PR #425 https://github.com/SamboyCoding/Cpp2IL/pull/425  
- Unity 6 Mach-O `codereg/metareg=0` 同类 issue（iOS/加密场景，**机制类似但根因未必相同**）：https://github.com/SamboyCoding/Cpp2IL/issues/500  

**一句话差异（证据驱动，不是「Mac 没人做」）：**  
Windows/Deck 跑在 **PE GameAssembly** 上，Cpp2IL 能定位 registration；Mac 原生跑在 **Mach-O**，registration 结构在 **BSS（运行时填充）**，静态磁盘镜像搜不到 `metareg`，而现有 QoL 生态又绑在 **winhttp PE 代理**上——所以「能注入插件」≠「能生成/加载托管 interop」。本仓库 Task1/3 已证明前者，Task4 证明后者失败。

---

## 3. Two Point 系列引擎 / Unity 代际

| 作品 | 公开线索 | 与 Museum 关系 |
|------|----------|----------------|
| **Two Point Hospital**（2018） | Unity 引擎（Wikipedia）；Nexus 广泛用 **Unity Mod Manager**（典型 **Mono** 管理程序集路径）；UMM 支持列表含 TPH，并有 **Linux/Mac** 安装说明（`mono Console.exe`） | **不是** 同一代 IL2CPP/Unity 6。精确 Editor 版本 **未验证**（无本机 `globalgamemanagers`） |
| **Two Point Campus** | 官方内容 Modding 用 Unity **2020.3.47f1**（与 Museum **内容 SDK** 同代文档）；运行时是否 IL2CPP **未验证**（公开强证据不足；勿把内容 SDK 版本等同玩家包） | 与 Museum **运行时**（Unity 6000 IL2CPP）很可能不同代 |
| **Two Point Museum** | 运行时 **Unity 6000.0.x + IL2CPP + metadata v31**（Mac 本机 + Windows #1109）；内容 Workshop SDK 仍指引 **2020.3.47f1** | 代码模面对的是 **Unity 6 IL2CPP**；装饰模走旧 Editor SDK |

链接：

- UMM（含 Two Point Hospital + Mac 说明）：https://www.nexusmods.com/site/mods/21  
- Campus 官方 Initial Modding Setup：https://mod.io/g/twopointcampus/r/initial-modding-setup  
- Museum Windows BepInEx 日志 Unity 6000.0.37f1：https://github.com/BepInEx/BepInEx/issues/1109  

**结论：** 系列共用「双点」品牌与部分内容管线，**不能**假定 Hospital/Campus 的 Mac Mono/UMM 经验可直接搬到 Museum IL2CPP。Hospital 的 Mac 模先例对 **注入哲学** 有参考价值，对 **Cpp2IL/Il2CppInterop** 几乎无直接帮助。

---

## 4. 其它双点游戏的 Mac 原生 mod loader 先例

| 先例 | 平台 | 说明 | URL |
|------|------|------|-----|
| **Unity Mod Manager ↔ Two Point Hospital** | Windows + **Mac/Linux 文档** | Mac：`mono Console.exe`；Apple Silicon 用 Rosetta；Doorstop/`run.sh` | https://www.nexusmods.com/site/mods/21 |
| TPH Nexus 模（依赖 UMM） | 主要为 Win；装载器层支持 Mac | 例：Qualification Utils https://www.nexusmods.com/twopointhospital/mods/10 | 同上生态 |
| SeanB Museum Loaders | **仅** Windows / Steam Deck(Proton) | 明确 *Not … Macbook* | `3817145646` / `3819798898` |
| Museum 官方 Workshop | 全平台内容模 | **无** 代码注入 loader | FAQ 见上 |
| Thunderstore「Two Point Museum Mac BepInEx」 | — | **未找到**（2026-10-19 检索） | — |
| MelonLoader macOS IL2CPP | 通用实验 | PR #900：il2cpp 很 fragile；与 Papers Please/TUNIC 等有关，**非** Two Point 成功案例 | https://github.com/LavaGang/MelonLoader/pull/900 |

**结论（证据）：** 双点系列里，**仅 Hospital 一代有文档化的 Mac 原生（Mono/UMM）代码模路径**；Museum **没有** 公开 Mac 原生代码 loader 成功先例。本仓库自建 BepInEx Mac Loader（Task1/3）属于 **探路**，不是跟抄现成工坊包。

---

## 5. 工具链修复线索（Cpp2IL / Il2CppInterop / BepInEx × Mach-O / Unity 6）

| 线索 | 状态 | 对 TPM Mac |
|------|------|------------|
| Cpp2IL pre.15+ metadata v31；pre.20 Mach-O chained fixups + universal；pre.21 支持更高 metadata / Unity 6000.5 alpha | **已发布**；本机已用 **pre.21** 仍失败 | **不能**指望「再升一级官方包」立刻过门禁（除非有 **更新的未验证** 提交） |
| `development` 分支至 2026-09 仍有提交，但公开 changelog **未见**「Mach-O BSS 内 MetadataRegistration 静态还原」专项修复 | **未验证**是否含隐式修复 | 可择机用 HEAD 复测，**不**作为主路径 |
| Cpp2IL #500 Unity 6 Mach-O `codereg/metareg=0` | **open**（2025-11 → 2026-01 评论疑 encryption） | 症状同类；TPM Steam Mac **未声称加密**，更像 BSS/重定位问题 |
| Il2CppInterop #206 / PR #272 macOS `GameAssembly.dylib` 基址 | #206 open；#272 **merged** 2026-06 | 若将来有 interop DLL，运行时仍可能依赖此修复是否进入所用 BE |
| BepInEx #1109 TPM Windows interop 生成失败 | open；关联 PR #262 **open** | 说明 **即便 PE 找到 registration，Il2CppInterop 仍可能挂**——Windows PE 交叉生成路线要预留第二道坑 |
| 官方兼容表 OSX IL2CPP ❌ vs BE 提供 macos-x64 包 | 并存 | 实验构建可用（本仓库 A 已通），不代表 interop 生成可用 |

Cpp2IL releases：https://github.com/SamboyCoding/Cpp2IL/releases  
development commits：https://github.com/SamboyCoding/Cpp2IL/commits/development/

---

## 6. 三条候选路径（证明进程内托管 / 等价调用）

### 6.1 路径 1 — 运行时 dump 已初始化进程的 `codereg`/`metareg`，再喂 Cpp2IL / Il2CppDumper

**机制简述**  
IL2CPP 在启动时把 CodeRegistration / MetadataRegistration 写入 **BSS**。磁盘 `GameAssembly.dylib` 该段 `filesize=0`，静态搜索必然空；进程初始化后内存里有完整指针表。从**已正常启动且 Loader 已附着**的进程读出地址（或 dump 含 BSS 的映像），再用 Cpp2IL `allowUserToInputAddresses` / Il2CppDumper manual mode 生成 Dummy DLL → Il2CppInterop。

**前置条件**

- A 侧 Loader 已通（**已满足**）。  
- 能在游戏进程内或调试器中定位两地址（模式：type count `0x78FA`、mscorlib codegen 链等——与 Cpp2IL verbose 日志同族）。  
- 不对抗 Denuvo：仅读**本进程**已映射内存 / 用户授权调试，不替换 player、不脱壳。

**社区先例 / 工具**

- Il2CppDumper manual CodeRegistration/MetadataRegistration：https://github.com/Perfare/Il2CppDumper/ ；排障 https://deepwiki.com/Perfare/Il2CppDumper/7.1-troubleshooting  
- 内存 dump 思路（多为 Android ELF，**Mach-O 同类原理未在 TPM 验证**）：https://github.com/MlgmXyysd/IL2CPPMemoryDumper  
- Cpp2IL 支持手动地址输入（API `allowUserToInputAddresses`）：https://github.com/SamboyCoding/Cpp2IL/blob/development/Cpp2IL.Core/README_CORE.md  
- Cpp2IL #127 讨论 dumped 文件（Android）：https://github.com/SamboyCoding/Cpp2IL/issues/127  

**对 TPM Mac 适用性：** **高（优先）** — 直接对准本机「BSS / metareg=0」根因；不依赖 Windows 包；保持 Il2CppInterop 插件模型。

**风险**

- 定位地址可能需若干轮探针；SIP/Hardened Runtime 可能限制外部调试器（**未验证**对本 Steam 应用的具体策略）。  
- 生成成功后仍可能撞上 Il2CppInterop XrefScan 类问题（#1109 类，**未验证** Mac）。  
- dump 产物体积大；地址随 ASLR/版本变化，需可复现脚本而非一次性手填。

**门禁标准**

1. 记录非零 `codereg`/`metareg`（文件或日志）。  
2. Cpp2IL 对 Mac 切片初始化 Binary 成功，产出 Dummy DLL。  
3. Il2CppInterop 生成至少核心程序集无致命错。  
4. **最小成功（默认规格）：** 插件在进程内解析到**任意一个**游戏托管类型或字段并写入 `LogOutput`（见 §8）。

**对 A/B 边界影响：** A 不变；B 恢复「引用 interop 的托管插件」模型。

---

### 6.2 路径 2 — 同版本 Windows PE `GameAssembly` + 同份 metadata 生成 interop；Mac 仅作运行时宿主

**机制简述**  
#1109 证明：Windows PE 上 Cpp2IL **能**找到 TPM 的 registration。若 `global-metadata.dat` 与 Mac 同 build 一致（或可接受差异），可在离线用 Windows `GameAssembly.dll` 跑通 Cpp2IL→（尝试）Il2CppInterop，再把生成的 DLL 拷到 Mac BepInEx。

**前置条件**

- 用户能取得**同版本** Windows 安装文件（本机 Windows、或 SteamCMD `@sSteamCmdForcePlatformType windows` 拉 depot——需拥有游戏）。  
  SteamCMD 跨平台拉 Win 文件的公开做法：https://swissmacuser.ch/download-install-steam-game-files-on-mac-os/  
- **仅用 Mac 已有文件不够**：Mac 侧没有 PE `GameAssembly.dll`。  
- Token/签名假设（**部分未验证**）：  
  - metadata 内容跨平台往往同源（类型/方法定义）→ 可用 SHA-256 比对 Mac vs Win `global-metadata.dat`；  
  - **方法指针 RVA 绑定生成时的 PE 映像** → 直接把 Win 生成的 interop 丢进 Mac **很可能**调用错误地址（Il2CppInterop 生成写入 NativeMethodInfoPtr）。**未验证**是否存在「仅用 Dummy 类型、运行时全靠 IL2CPP API 解析」的生成模式可规避。

**社区先例**

- #1109：Win Cpp2IL OK，Il2CppInterop 生成仍挂。  
- Il2CppInterop CLI 要求 `--game-assembly` 指向**用于抽指针**的二进制：https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Command-Line-Usage.md  
- **未找到**「Win 生成 interop → Mac 宿主成功」的公开 TPM 案例。

**适用性：** **中低** — 作为 **Cpp2IL 能跑通的对照实验**很有价值；作为「Mac 可加载的最终 interop」**高不确定**。离线分析 **不等于** 对抗 Denuvo（不替换启动、不脱壳）。

**风险**

- 第二道坑：XrefScan（#1109）。  
- RVA/平台 ABI 不一致导致静默坏调用。  
- 版本漂移（Mac `6000.0.63f1` vs 旧报告 `37f1`）必须对齐。

**门禁标准**

1. Win/Mac metadata 哈希对比报告。  
2. Win PE 上 Cpp2IL Binary init 成功。  
3. （若强行拷 Mac）进程内调用门禁与 §8 相同——**失败则记「生成成功但运行时不可用」**，不得标总成功。

**对 A/B 边界：** A 仍用 Mac Loader；B 依赖外来 interop 工件与版本锁定流程。

---

### 6.3 路径 3 — 放弃托管 interop，改 IL2CPP C API / 纯原生钩子

**机制简述**  
`GameAssembly.dylib` 导出 `il2cpp_*` C API（`il2cpp_domain_get`、`il2cpp_class_from_name`、`il2cpp_runtime_invoke` 等）。用 P/Invoke 或原生 dylib，按**名字/token**解析类型与方法并调用。  
重要：**Cpp2IL 已成功读 metadata**（30970 types）→ 类型/方法名列表可不依赖 metareg；缺的是 method 指针表与完整 Dummy/interop。

**前置条件**

- 确认 dylib 导出符号（`nm` / `dlsym`；task4 已对 Registration **无导出**，但 `il2cpp_*` 通常仍导出——**对本机导出表未在本报告重扫，标待验证**）。  
- 从 metadata 或公开字符串得到至少一个稳定类型名做探针。

**社区先例**

- Unity `il2cpp-api-functions.h` 公开 API 面（多版本镜像）：例 https://github.com/4ch12dy/il2cpp/blob/master/unity_2017_x/libil2cpp/il2cpp-api-functions.h  
- BepInEx IL2CPP chainloader 自身即 hook `il2cpp_runtime_invoke`：https://github.com/BepInEx/BepInEx/blob/master/Runtimes/Unity/BepInEx.Unity.IL2CPP/IL2CPPChainloader.cs  
- 原始指针 / 少 interop 包装的实验项目（质量不一）：检索可见 IL2CppSharp 等（**未验证**可用性）

**适用性：** **中高（并行烟雾）** — 最快证明「等价调用」；牺牲托管 DX。适合 §8 最小门禁。

**风险**

- API/ABI 随 Unity 6 变化；字符串混淆则更难。  
- 无 Il2Cpp 类型系统 → 后续玩法成本高（但本调研**不做玩法**）。  
- 改写 B 插件约定（不再 `using Il2Cpp...`）。

**门禁标准**

1. P/Invoke 或 native 成功 `il2cpp_class_from_name`（或等价）拿到非空 `Il2CppClass*`。  
2. 读一个字段或 `il2cpp_class_get_name` 打到 BepInEx 日志。  
3. 满足 §8 最小成功定义。

**对 A/B 边界：** A 可保留；B 从「Il2CppInterop 插件」改为「C API 探针插件」或混合。规格成功标准 3（若原规格写死 interop）需修订。

---

## 7. Windows PE 同 metadata 路线 — 专项结论

| 问题 | 结论 |
|------|------|
| token/签名一致性 | **假设**：同 Steam build 的 `global-metadata.dat` 跨 Win/Mac 应一致或近似 → **用哈希验证**；不一致则禁止混用。方法表 RVA **不**跨 PE/Mach-O 移植（**未验证反例**）。 |
| 是否需要用户有 Windows 拷贝 | **要有 Win 的 `GameAssembly.dll`（+ 建议同包 metadata）**。完整 Windows 安装 **或** SteamCMD 强制 windows 平台拉 depot（须拥有游戏）。**仅 Mac 公开已装文件不够。** |
| 仅用公开 Steam 文件是否可行 | Mac 已购游戏 → 可用 SteamCMD 拉 Win depot（所有权内）。**不要**用替换 `WindowsPlayer.exe` 绕 Denuvo 的 #1109 复现步骤（违反非目标）。离线 Cpp2IL 读 DLL **本身**不构成对抗 Denuvo。 |
| 推荐角色 | **对照实验 / 备选**，不是第一推荐。 |

---

## 8. 推荐优先级 + 规格可用成功标准

### 推荐排序

| 优先级 | 路径 | 理由 |
|--------|------|------|
| **P0 推荐** | **§6.1 运行时 dump / 手动 registration → Cpp2IL → Il2CppInterop** | 对准 BSS 根因；纯 Mac；保住托管插件模型；A 已通 |
| **P1 备选** | **§6.3 IL2CPP C API 最小探针** | 不依赖 metareg；用已可读的 metadata 名字；最快证明「等价调用」；改 B 边界但满足默认成功标准 |
| **P2 备选** | **§6.2 Windows PE 离线生成（对照）** | #1109 证明 PE 上 registration 可得；但 interop 生成与跨平台 RVA **双风险**；需用户拍板是否拉 Win 文件 |

**明确不做：** Denuvo 绕过、CrossOver、多选玩法、等待无证据的 upstream「BSS 奇迹修复」作为唯一计划。

### 规格草稿可用的成功标准候选（默认：最小托管/等价读）

1. **在 Mac 原生 TPM 进程内**（已附着现有 BepInEx Loader），插件日志出现对**至少一个**游戏侧托管类型或字段的解析结果（类型名 / 字段值 / `Il2CppClass` 名三者任一即可）。  
2. 该结果必须来自 **Il2CppInterop 托管调用** 或 **IL2CPP C API 等价调用**（二选一写进规格选定路径）；禁止用「仅 Loader 启动成功」冒充。  
3. 复现材料：`BepInEx/LogOutput.txt` 摘录 + 所用路径编号（6.1 / 6.3 / 6.2）+ Unity/metadata/工具版本；全流程**不**替换玩家可执行文件、**不**对抗 Denuvo。

---

## 9. 扩展：推荐方案与备选（面向规格开工）

### 推荐方案（P0）— 运行时 registration → 托管 interop 最小读

1. 保持 `UpdateInteropAssemblies=false`，游戏进主菜单后由探针插件或外部调试器定位 `codereg`/`metareg`。  
2. 喂 Cpp2IL（pre.21 或更新 HEAD **复测**）生成 Dummy → Il2CppInterop。  
3. 最小插件：`Il2Cpp.` 下任意稳定类型 `typeof` / 读静态字段 / `ToString` 打日志。  
4. **门禁 = §8 三条。**

### 备选 1（P1）— C API 探针

1. 确认 `il2cpp_*` 导出；P/Invoke。  
2. 用 metadata 已知字符串 `il2cpp_class_from_name`。  
3. **门禁 = §8**（标记路径 6.3）。

### 备选 2（P2）— Win PE 对照（需用户拍板）

1. 用户确认可 SteamCMD/拷贝同版本 Win `GameAssembly.dll`。  
2. 哈希比对 metadata；Cpp2IL on PE；尝试生成；**仅当** Mac 进程内 §8 通过才算成功。

---

## 10. 需要用户拍板的前提

1. **是否拥有 / 是否愿意拉取同版本 Windows TPM 文件？**（决定 P2 是否开工；不影响 P0/P1）  
2. **最小成功定义：** 已按用户跳过卡片后的默认——「进程内读到任意一个游戏托管类型/字段并打日志」。若要改为「必须是 Il2CppInterop 而非 C API」，需明示（会影响 P0 vs P1 的「成功」判定是否互认）。  
3. **外部调试器 / 进程内存读取：** 是否允许为 P0 使用 lldb 等（若 SIP 阻碍则优先进程内探针）。

---

## 11. 来源索引（访问日均为 2026-10-19，除非另注）

| 来源 | URL |
|------|-----|
| 本地 task4 门禁 | `docs/task4-interop-gate.md` |
| Cpp2IL releases | https://github.com/SamboyCoding/Cpp2IL/releases |
| Cpp2IL #500 | https://github.com/SamboyCoding/Cpp2IL/issues/500 |
| Cpp2IL universal Mach-O PR #425 | https://github.com/SamboyCoding/Cpp2IL/pull/425 |
| BepInEx #1109 TPM Win interop | https://github.com/BepInEx/BepInEx/issues/1109 |
| Il2CppInterop #206 / PR #272 / PR #262 | github.com/BepInEx/Il2CppInterop |
| BepInEx IL2CPP 安装 / Proton | docs.bepinex.dev |
| SeanB Win Loader | https://steamcommunity.com/sharedfiles/filedetails/?id=3817145646 |
| SeanB Deck Loader | https://steamcommunity.com/sharedfiles/filedetails/?id=3819798898 |
| TPM-Mod-Engine | https://github.com/coding-dex/TPM-Mod-Engine |
| Nexus TPM Mod Loader | https://www.nexusmods.com/twopointmuseum/mods/18 |
| UMM（含 TPH + Mac） | https://www.nexusmods.com/site/mods/21 |
| Museum Community FAQ | https://community.twopointcounty.com/two-point-studios/two-point-museum/blogs/82-museum-community-faq |
| Il2CppDumper | https://github.com/Perfare/Il2CppDumper |
| MelonLoader macOS PR #900 | https://github.com/LavaGang/MelonLoader/pull/900 |

---

*报告结束。实现与规格撰写交由建造 / 规格角色；本文件只提供可引用的调研结论与门禁措辞。*
