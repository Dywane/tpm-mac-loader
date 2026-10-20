# 验收报告：tpm-mac-loader-host（host-plus-smoke）

- **结论：通过**
- **验收方：** demo验收
- **验收时间：** 2026-10-20 10:52–10:54 Asia/Shanghai（新鲜冷启动）；静态核对同日约 10:50
- **切片：** host-plus-smoke（宿主 + 多选依赖冒烟；**不开**玩法）
- **规格：** `/Users/weidu/Documents/Project/TPM-muti-select/docs/specs/tpm-mac-loader-host-spec.md` §4 成功标准 1–6
- **计划：** `docs/plans/tpm-mac-loader-host-plan.md`
- **门禁：** `docs/loader-host-gate.md`（建造方已勾；本报告为**独立复验**，不采信勾选）
- **仓库 HEAD（验收时）：** `5104481`（轨 A）；loader-host 相关祖先 `af3b27e`、`6dd057a` 均在历史中
- **权威副本路径：** 本文件 → `/workspace/demo-lab/parity/tpm-mac-loader-host-verify.md`；同步至本机仓库 `docs/parity/tpm-mac-loader-host-verify.md`

---

## 总表（规格 §4）

| # | 成功标准 | 结果 | 证据 |
|---|----------|------|------|
| 1 | Loader+Host 单独可用 | **通过** | 场景①新鲜冷启动 |
| 2 | 多选冒烟依赖链 | **通过** | 场景②新鲜冷启动 |
| 3 | 缺 Host 不静默生效 | **通过** | 场景③新鲜冷启动（Chainloader missing dep） |
| 4 | 工程拆分（loader / mac-il2cpp-host / multiselect 分树；安装只拷产物） | **通过** | 静态核对 |
| 5 | 文档两章（只装 Loader+Host；再装多选并声明依赖） | **通过** | 静态核对 `README.md` ①② |
| 6 | 约束遵守（无玩法；不停轨 A；不默认 capi-probe；无 Win 直拷） | **通过** | 静态核对 |

**补充核对（验收单重点）：**

| 项 | 结果 |
|----|------|
| Host GUID = `com.tpm.mac.il2cpp-host` | **通过**（源码 `HostIds.cs` + 日志 `GUID=com.tpm.mac.il2cpp-host`） |
| `install-loader.sh` 部署 Host、不默认装 capi-probe | **通过**（脚本 + 安装后 plugins 列表） |
| 无玩法恢复 / 不停轨 A | **通过**（Multiselect 仅 smoke；`interop-probe` 仍在） |

---

## 场景① — Loader+Host 单独可用 → **通过**

- **做法：** `./loader/scripts/install-loader.sh`（exit 0）；将 `TpmMacMultiselect.dll` 移出 plugins；`cd "$GAME" && arch -x86_64 ./run_bepinex.sh`；见 `Chainloader startup complete` 后结束进程。
- **plugins（当时）：** `TpmMacIl2CppHost.dll` + `TpmMacInteropProbe.dll`；**无** Multiselect；**无** `TpmMacCapiProbe.dll`
- **命中行（新鲜）：**
  - `TPM Mac IL2CPP Host 0.1.0 loaded (GUID=com.tpm.mac.il2cpp-host).`
  - `[HOST] Ready; HostFacade set`
  - `[HOST] class_from_name ok ns=System name=String ptr=0x109765390 got=System.String`
  - `Chainloader startup complete`
- **新鲜证据：** `docs/evidence/verify-loader-host-scene1-x64-20261020105309.log`
- **建造方旧证据（对照，非本轮判定依据）：** `docs/evidence/loader-host-host-smoke-script-x64.log`

---

## 场景② — Host+Multiselect 依赖链 → **通过**

- **做法：** 还原 Multiselect 至 plugins；确认 Host 在；冷启动同上。
- **命中行（新鲜）：**
  - `Loading [TPM Mac IL2CPP Host 0.1.0]` / `[HOST] Ready; HostFacade set`
  - `Loading [TPM Mac Multi-Select 0.1.0]`
  - `[MS] host ready / smoke ok`
  - `Chainloader startup complete`
- **新鲜证据：** `docs/evidence/verify-loader-host-scene2-x64-20261020105355.log`
- **建造方旧证据（对照）：** `docs/evidence/loader-host-multiselect-smoke-x64.log`

---

## 场景③ — 缺 Host 负例 → **通过**

- **做法：** `mv TpmMacIl2CppHost.dll` → `BepInEx/.verify-host-aside/`；保留 Multiselect；冷启动；测后已 `mv` 恢复 Host。
- **命中行（新鲜）：**
  - `Could not load [TPM Mac Multi-Select 0.1.0] because it has missing dependencies: com.tpm.mac.il2cpp-host. Install the listed plugin(s) and restart the game.`
  - **无** `[MS] host ready / smoke ok`
  - `Chainloader startup complete`
- **失败信号形态：** Chainloader 依赖机制（HardDependency）
- **新鲜证据：** `docs/evidence/verify-loader-host-scene3-x64-20261020105441.log`
- **建造方旧证据（对照）：** `docs/evidence/loader-host-missing-host-negative-x64.log`

---

## 静态项摘要（标准 4–6 等）

| 项 | 结果 | 要点 |
|----|------|------|
| 工程拆分 | 通过 | `loader/`、`plugins/mac-il2cpp-host/`、`plugins/multiselect/` 分树；`loader/` 内无 Host 源码/DLL；install 只 `cp` `bin/Release/TpmMacIl2CppHost.dll` |
| 文档 ①② | 通过 | `README.md` 含「① 只装 Loader（含 Host）」「② 再装多选（依赖 Loader + Host）」 |
| GUID | 通过 | `HostIds.Guid = "com.tpm.mac.il2cpp-host"` |
| HardDependency | 通过 | `[BepInDependency(HostIds.Guid, HardDependency)]` + `HostFacade.Ready` 冒烟 |
| install / 禁 capi-probe | 通过 | 缺 Host 产物 `exit 1`；注释明确不拷 capi-probe；残留 WARN |
| 无玩法 | 通过 | Multiselect Load 仅 Ready/smoke；未接 Shift / `IItemSelectionBridge` |
| 不停轨 A | 通过 | `plugins/interop-probe/` 仍在；游戏 plugins 有 `TpmMacInteropProbe.dll`（本轮日志 0.5.34） |
| 无 Win 直拷 | 通过 | Host 仅 `DllImport("libSystem.B.dylib")`（dlsym） |

---

## 验收后环境

游戏 `BepInEx/plugins/` 已恢复：

- `TpmMacIl2CppHost.dll`
- `TpmMacInteropProbe.dll`
- `TpmMacMultiselect.dll`
- **无** `TpmMacCapiProbe.dll`

---

## 范围外（本单不验）

- 轨 A 调用桥（FYI：建造方报 `5104481`）— **另开验收**
- 多选玩法（Shift 拾取/旋转/删除）— 规格明确非目标

---

## 判定

规格 §4 成功标准 1–6 均有**本轮新鲜证据**或**本轮静态命令输出**支撑 → **通过**。  
建造方门禁勾选与本轮独立复验一致；本报告以复验证据为准。
