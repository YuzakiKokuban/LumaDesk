# 构建与发布

Windows x64，Python 3.12 或更新版本、Rust 1.92.0 MSVC、.NET SDK 10.0.401、Visual Studio C++ 构建工具和 Windows SDK。工具链由 `rust-toolchain.toml`、`global.json` 固定。依赖由 Cargo.lock 及 Debug/Release 各自的 NuGet 锁文件固定。Rust 的 Windows 资源也调用同一 Python 版本解析器。

## 本地构建

```powershell
cargo build --locked
dotnet restore app/JiYaoChu.csproj --locked-mode -p:Configuration=Debug
dotnet build app/JiYaoChu.csproj -c Debug --no-restore
```

先构建 Rust DLL，再构建 WinUI。调试入口：`app/bin/Debug/net10.0-windows10.0.26100.0/win-x64/LumaDesk.exe`。

```powershell
./scripts/build.ps1
python scripts/verify-package.py --archive artifacts/LumaDesk-0.2.0-beta.5-win-x64.zip
```

构建脚本需要 Python 3.12 或更新版本。`scripts/version.py` 从 Cargo.toml 读取本地版本，自动同步 Cargo.lock 根包、C# 产品版本和 Windows 文件版本；不更新依赖。`build.ps1 -Version <version>` 可以显式指定并同步构建版本。安装器版本和 SHA256 文件名均由同一入口生成。

发布脚本先强制重建正式 Release 宿主，避免模拟测试宿主的权限清单被增量复用，再构建便携 ZIP、Inno Setup 安装 EXE 和各自的 SHA256 文件。安装器编译器固定为 Inno Setup 7.1.0，并校验下载哈希与签名。应用共用系统 .NET 与 Windows App SDK 运行时，依赖下载见 [RUNTIMES.md](RUNTIMES.md)。

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

[Build and verify](../.github/workflows/ci.yml) 在 main/dev 推送、目标为 main/dev 的 PR 及手动运行时调用三个可复用工作流：

1. [质量检查](../.github/workflows/checks.yml)：Rust 格式、Clippy、单元与 FFI 测试、Python 协议及发布工具测试、actionlint、发布依赖关系检查。
2. [依赖审计](../.github/workflows/dependency-audit.yml)：RustSec 与 Debug/Release NuGet 直接、传递依赖检查；每周一北京时间 10:25 也会独立检查默认分支。
3. [Windows 构建](../.github/workflows/build.yml)：锁定依赖还原、Debug 编译、Release 打包、包检查、设置体验、OSD、自启动迁移、安装和卸载验证，上传产物及验证报告。

`Windows x64` 保持原有分支保护检查名称，汇总上述结果；失败、取消和跳过都不能算通过。Rust 缓存按检查和打包职责复用 main/dev 的结果，PR 与标签不写入一次性 Rust 缓存。ZIP/安装器 artifact 关闭外层压缩。

推送 `v*` 标签后，由[发布工作流](../.github/workflows/release.yml)验证标签语义、精确提交与 main/dev 来源，再以标签版本重新执行全部检查、审计和打包。标签版本可以与 Cargo.toml 的开发版本不同，构建时自动注入并仅同步根包锁文件。下载产物后再次校验四个预期文件及 SHA256，全部通过才发布 GitHub Release；发布任务不中途取消。

发布版本接受 `X.Y.Z` 以及 `X.Y.Z-alpha.N`、`X.Y.Z-beta.N`、`X.Y.Z-rc.N`（N 为 1–9999），拒绝前导零和 build metadata。Windows 文件版本使用第四段表示阶段：alpha 为 10000+N、beta 为 20000+N、rc 为 30000+N、正式版为 65535，保证同版本的正式版高于预览版。

git-cliff 依据 cliff.toml 从标签提交生成更新日志；发布说明组合人工说明与提交清单。优先读取 `docs/releases/<version>.md`，否则读取 `docs/releases/NOTES.md`。人工说明仍需维护支持机型、实机验证范围、已知限制与升级注意事项。CI 不自动修改远程 dev，也不发送第三方通知。工作流使用隔离模拟后端，不访问开发者机器的 OEM 服务或 EC。

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

## 设置体验回归

使用上述隔离 asInvoker 宿主，在运行 verify-shell.ps1 前设置 `JIYAOCHU_VERIFY_SETTINGS=1`，验证控件保持挂载、滚动位置、局部读回、自动档位和导出。本地会保存全部页面的屏幕截图；无可交互桌面时可设置 `JIYAOCHU_VERIFY_SCREENSHOTS=0`，仍检查原生控件树和布局。`JIYAOCHU_VERIFY_MEMORY_IDLE=1` 额外等待清理冷却期，记录持续空闲后的工作集。

`python scripts/verify-notification-recovery.py --library target/release/jiyaochu_core.dll --output artifacts/notification-recovery-validation.json` 被动订阅本机 WMI 并验证重连，不能证明实际 Fn 操作或睡眠恢复。它不写 EC，也不改变亮度。
