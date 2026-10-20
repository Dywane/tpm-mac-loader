# Task 4 — IL2CPP interop 门禁结果：**失败**

日期：2026-10-19（Asia/Shanghai）  
环境：原生 Mac Steam TPM（AppID 2185060），Unity `6000.0.63f1`，`global-metadata` **version 31**  
工具：Cpp2IL `2022.1.0-pre-release.21`（OSX x86_64 under Rosetta；ARM64 发布包为损坏 apphost，不可用）

## 4.1 interop 生成

| 二进制 | 结果 |
|--------|------|
| `lipo -thin x86_64` → `GameAssembly-x64.dylib` | `codereg: 0x34CC0D0`，**`metareg: 0x0`** → `Failed to find code registration or metadata registration` |
| `lipo -thin arm64` → `GameAssembly-arm64.dylib` | `codereg: 0x0`，`metareg: 0x0` |

证据：

- `docs/evidence/task4-cpp2il-pre21-x64.log`
- `docs/evidence/task4-cpp2il-pre21-arm64.log`

补充：x64 上 Cpp2IL 推得的 `codereg` 落在 Mach-O **`__bss`**（`va≈0x34cbd10`，`filesize=0`），磁盘镜像无该结构初值；静态分析无法读出有效 MetadataRegistration。与 Task 1 冒烟结论一致（旧版 Cpp2IL 亦 `metareg=0`）。

已尝试 / 排除：

- 换用截至当日最新 Cpp2IL pre-release.21（仍失败）
- 分别分析 universal 的 x64 / arm64 切片
- `nm`：无 `MetadataRegistration` / `CodeRegistration` 导出符号
- **未**改用 CrossOver / Windows 注入；**未**对抗 Denuvo（符合计划失败回退）

BepInEx 侧：为避免卡死，`UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false`（见既有 Loader 配置）。开启自动生成时亦会走同一 Cpp2IL 路径失败。

## 4.2 最小托管调用

**未执行** — 依赖 4.1 生成的 interop 程序集。无可用托管类型桥，无法做「经游戏类型解析」的无害读字段/日志。

## 4.3 game-hooks

见 `docs/game-hooks.md`：仅记录门禁阻塞；**无已验证**游戏选择/建造 API。

## 计划回退（已执行）

按 `docs/plans/tpm-mac-multiselect-plan.md` Task 4 失败回退与风险表：

- **停** Task 5–9 玩法实现
- 本文件 + evidence + `game-hooks.md` 记录现象
- **不**换 CrossOver；规格需修订（Mac Unity 6 Mach-O IL2CPP interop 静态生成不可用）

## 规格修订候选（供计划架构 / 脑暴，非本角色改规格）

1. 运行时从已初始化进程 dump `codereg`/`metareg`（BSS 填充后）再喂 Cpp2IL 手动地址；或  
2. 用同版本 Windows `GameAssembly` + 同份 metadata 生成 interop（若 token 一致），Mac 仅作运行时宿主；或  
3. 放弃托管 interop，改 IL2CPP C API / 纯原生钩子（会改 A/B 边界与规格成功标准 3）。
