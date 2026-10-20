# Task 1 — Mac Museum Mod Loader 冒烟（2026-10-19 Asia/Shanghai）

## 判据

规格成功标准 **1**：仅装 Loader 时，BepInEx 日志显示宿主已加载（无致命错误）；不改多选。

## 结果：**通过（A 宿主冒烟）**

| 项 | 证据 |
|----|------|
| 钉死包 | `BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286`（`loader/VERSION.txt`） |
| 启动 | `arch -x86_64` + Doorstop（`run_bepinex.sh`）；Steam 保持运行 |
| LogOutput | `docs/evidence/task1-LogOutput.log`：`Chainloader initialized`；Unity `6000.0.63f1`；进程 x64 |
| 游戏 | Player.log：`Loading Main Scene` / `Main scene load took …`；进程存活；`lsappinfo` 显示 Two Point Museum |
| 插件 | `BepInEx/plugins/` 空；无 Multi-Select |

> **踩坑：** 必须在游戏根目录执行 `./run_bepinex.sh`。`executable_name=TPM.app` 为相对路径；`sh /全路径/run_bepinex.sh`（cwd 不在游戏根）会报 `Please set executable_name to a valid name…`。正确：`cd "$GAME" && ./run_bepinex.sh`。

## Mac 布局修复（装入 install-loader.sh）

BepInEx `Paths` 在 macOS 把 GameRoot 定为可执行文件上溯 4 级（含 `TPM.app` 的目录），并查找 `ProcessName_Data` 或 `Data`，以及根目录 `GameAssembly.dylib`。TPM 实际为：

- Data → `TPM.app/Contents/Resources/Data`
- GameAssembly → `TPM.app/Contents/Frameworks/GameAssembly.dylib`

安装脚本创建对应软链。缺 Data 软链时 Preloader：`ArgumentNullException(gameDataPath)`。

## 已知限制（不阻断 Task 1；阻断 Task 4 interop）

自动 Cpp2IL 在 Mach-O `GameAssembly.dylib` 上：`codereg` 有值、`metareg=0` → 无法生成 interop。冒烟时将 `UpdateInteropAssemblies=false` / `PreloadIL2CPPInteropAssemblies=false` 以免卡在生成失败。IL2CPP 插件 Invoke 钩子能否在 Mac 完整跑通 → **Task 4** 再验；失败则按计划停在 A 门禁后的 B 门禁，不换 CrossOver。

## 1.4 原版手感

未安装多选插件；无额外玩法 DLL。拾取/旋转/删除路径未注入逻辑（空 `plugins/`）。
