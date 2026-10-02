# 构建与发布

Windows x64，Rust 1.92.0 MSVC、.NET SDK 10.0.401、Visual Studio C++ 构建工具和 Windows SDK。工具链由 `rust-toolchain.toml`、`global.json` 固定。依赖由 Cargo.lock 及 Debug/Release 各自的 NuGet 锁文件固定。

## 本地构建

```powershell
cargo build --locked
dotnet restore app/JiYaoChu.csproj --locked-mode -p:Configuration=Debug
dotnet build app/JiYaoChu.csproj -c Debug --no-restore
```

先构建 Rust DLL，再构建 WinUI。调试入口：`app/bin/Debug/net10.0-windows10.0.26100.0/win-x64/机耀处.exe`。

```powershell
./scripts/build.ps1
python scripts/verify-package.py --archive artifacts/LumaDesk-0.1.2-win-x64.zip
```

发布脚本从 Cargo.toml 读取版本，检查前端版本一致后构建完整便携目录、ZIP 和 SHA256 文件。完整目录包含 .NET、Windows App SDK、Rust DLL、CLI、文档与许可证。验证脚本检查压缩包、应用与 CLI 的嵌入图标、管理员清单，以及模拟后端 CLI/FFI。

## CI/CD

[Build and release](../.github/workflows/ci.yml) 在 main 推送、PR、手动运行时执行：

1. Rust 格式、Clippy、单元与 FFI 测试。
2. Python EC/MUX 离线测试。
3. 锁定依赖还原、WinUI Debug 编译。
4. Release 发布、便携包验证和构建产物上传。

推送与源码版本相同的 `v*` 标签后，发布任务将已验证的 ZIP 与 SHA256 上传至 GitHub Releases。发布任务依赖所有检查通过。工作流使用模拟后端，不访问开发者机器的 OEM 服务或 EC。

## 实机验证

```powershell
python scripts/verify-hardware.py --library target/debug/jiyaochu_core.dll --output artifacts/hardware-validation.json
python scripts/verify-controls.py --library target/release/jiyaochu_core.dll --hold 6
python scripts/verify-oem-restore.py
```

控制脚本需要管理员权限，临时切换档位、Windows 模式、背光、充电上限、Win 键锁和强冷，完成后恢复原设置；按本机使用要求保留 OEM 接管。`--hold` 设置每档灯光的观察时间。MUX 验证脚本只读取，不自动切换或重启。

## 图标

`assets/LumaDesk.svg` 保留用户提供的原文件。`scripts/generate-icons.ps1` 生成 PNG 和七种尺寸 ICO，并同步应用图标；生成图标使用白色圆角矩形底衬，方便在深色任务栏辨认。

配置与日志位于 `%APPDATA%\JiYaoChu`，启动错误记录于 `ui-errors.log`。
