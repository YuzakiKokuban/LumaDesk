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
python scripts/verify-package.py --archive artifacts/LumaDesk-0.2.0-beta.2-win-x64.zip
```

发布脚本从 Cargo.toml 读取版本，检查前端版本一致后构建便携 ZIP、Inno Setup 安装 EXE 和各自的 SHA256 文件。安装器编译器固定为 Inno Setup 7.1.0，并校验下载哈希与签名。应用共用系统 .NET 与 Windows App SDK 运行时，依赖下载见 [RUNTIMES.md](RUNTIMES.md)。

验证脚本检查压缩包、应用与 CLI 的嵌入图标、管理员清单及模拟后端 CLI/FFI。`scripts/verify-installer.ps1` 在空目录执行安装、已安装 CLI、界面启动及卸载测试。

```powershell
python scripts/verify-win-key.py --library target/debug/jiyaochu_core.dll
python scripts/verify-installer-dependencies.py
python scripts/verify-autostart-migration.py
./scripts/verify-shell.ps1 -ApplicationDirectory app/bin/Debug/net10.0-windows10.0.26100.0/win-x64
```

按键测试在独立配置目录验证左右 Win 键的实际投递，不向其他应用发送按键。安装依赖测试执行安装器自身的检测脚本，覆盖缺 .NET、缺 WinUI、版本过旧、架构不匹配和两者均缺失，不卸载本机组件。自启动迁移测试执行实际脚本，以替身任务验证所有权判断和元数据保留，不改动 Task Scheduler。窗口测试使用模拟后端，覆盖后台启动、关闭/最小化收起、托盘打开、暂停遥测、OSD 焦点与超时、彻底退出，并保存预览图。

常规 EXE 的窗口测试需要管理员终端。普通终端可编译一个仅用于模拟验证的 asInvoker 测试宿主：

```powershell
$testManifest = Join-Path (Get-Location) 'artifacts/mock-shell.manifest'
[IO.File]::WriteAllText($testManifest, (Get-Content app/app.manifest -Raw).Replace('requireAdministrator', 'asInvoker'))
dotnet build app/JiYaoChu.csproj -c Debug -p:RestoreLockedMode=true "-p:ApplicationManifest=$testManifest" '-p:OutDir=D:\LumaDesk\artifacts\mock-shell-host\'
./scripts/verify-shell.ps1 -ApplicationDirectory artifacts/mock-shell-host
```

测试宿主的非管理员入口仅允许 `JIYAOCHU_FORCE_MOCK=1` 与 `--verify-shell` 同时使用。发布时采用原始 `app/app.manifest`，保持管理员权限要求。不要直接通过 dotnet 启动 UI DLL：原生资源查找依赖正确的应用宿主名称。

## CI/CD

分支职责、rebase 合并及版本发布约定见 [开发工作流](WORKFLOW.md)。正式版标签的提交必须属于 `main`，预览版必须属于 `dev`。

[Build and release](../.github/workflows/ci.yml) 在 main/dev 推送、目标为 main/dev 的 PR、版本标签推送及手动运行时执行：

1. Rust 格式、Clippy、单元与 FFI 测试。
2. Python EC/MUX 离线测试。
3. 锁定依赖还原、WinUI Debug 编译。
4. Release 发布、便携包与安装器验证、构建产物上传。

推送与源码版本相同的 `v*` 标签后，发布任务将已验证的 ZIP、安装 EXE 与 SHA256 上传至 GitHub Releases。带 `-beta` 等版本后缀的标签自动标为预发布。发布任务依赖所有检查通过，说明取自 `docs/releases/<version>.md`。工作流使用模拟后端，不访问开发者机器的 OEM 服务或 EC。

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
