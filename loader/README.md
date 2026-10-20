# Mac Museum Mod Loader（交付物 A）

钉死包装的 BepInEx 6 Unity IL2CPP **macOS x64**，给 Two Point Museum（Steam 原生 Mac）做插件宿主。

**本目录不含多选玩法。** 多选见仓库 `plugins/multiselect/`（交付物 B）。

## 钉死版本

见同目录 `VERSION.txt`：

- 包名：`BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.789+6cea286`
- 来源：https://builds.bepinex.dev/projects/bepinex_be

## 安装 / 卸载

```bash
# 默认游戏根目录：
# ~/Library/Application Support/Steam/steamapps/common/Two Point Museum
./scripts/install-loader.sh
./scripts/uninstall-loader.sh
```

可用环境变量 `TPM_GAME_ROOT` 覆盖游戏根路径。

`dist/` 只保留校验清单（`.sha256`）；完整 zip 由安装脚本下载到 `loader/.cache/`（不进 git）。

根 README **第①章**有完整启动与日志说明。
