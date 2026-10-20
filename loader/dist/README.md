# dist

不提交完整 BepInEx zip（体积大）。此处仅保留：

- `*.zip.sha256` — 与 `VERSION.txt` / `install-loader.sh` 中期望哈希一致

安装脚本把 zip 缓存到 `loader/.cache/`（gitignore）。
