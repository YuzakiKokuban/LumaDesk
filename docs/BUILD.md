# 构建与发布

适用于 Windows x64。实机开发环境为 Rust MSVC 工具链、.NET SDK 10.0.401。需要 Visual Studio C++ 构建工具和 Windows SDK。前端目标 `net10.0-windows10.0.26100.0`，优先在本机 Windows 11 验证；声明的较低系统版本尚未实测。

## 依赖

Rust 依赖由 Cargo.lock 固定。前端使用 Windows App SDK 2.5.1、SDK BuildTools 10.0.28000.2705、WinApp 0.7.0、Reactor 0.1.0-preview.16，项目文件已固定版本。Reactor 是预览依赖，编译通过不能替代运行验证。

## 调试

```powershell
cargo build
cargo test --lib
dotnet build app/JiYaoChu.csproj -c Debug
```

先构建 Rust，再构建前端。后端输出 `target/debug/jiyaochu_core.dll` 和 `jiyaochu-ctl.exe`，前端自动复制 DLL。启动 `app/bin/Debug/net10.0-windows10.0.26100.0/win-x64/机耀处.exe`。

## 发布

```powershell
./scripts/build.ps1
```

脚本生成 `artifacts/LumaDesk-0.1.1-win-x64/` 和 `artifacts/LumaDesk-0.1.1-win-x64.zip`，加入 CLI、文档、许可证。手动发布命令：

```powershell
cargo build --release
dotnet publish app/JiYaoChu.csproj -c Release -r win-x64 -o artifacts/LumaDesk-0.1.1-win-x64
```

这是完整目录形式的便携包，不是单文件 EXE。包含 .NET 和 Windows App SDK 运行依赖；设备驱动、NVIDIA 驱动由系统安装环境提供。构建不接管服务、不切换 MUX、不重启。

## 测试

```powershell
cargo test --lib
python -m unittest discover -s reverse/native -p 'test_*.py'
```

FFI 测试使用模拟后端。管理员实机诊断：

```powershell
python scripts/verify-hardware.py --library target/debug/jiyaochu_core.dll --output artifacts/hardware-validation.json
```

诊断读取固件和状态，临时翻转强冷位并在 finally 中恢复，只恢复自己修改的位。检查 `write_verified`、`restore_verified` 和错误字段；进程退出本身不算成功。脚本不修改 MUX、不重启、不接管服务。

启动失败检查 `%APPDATA%\JiYaoChu\ui-errors.log` 与 Windows 应用事件日志。写入需同时验证读回和实际行为。

管理员控制验证：`python scripts/verify-controls.py` 会暂时切换 OEM/Windows 模式和 RGB，最终恢复原 EC/Windows 状态；按用户要求保留官方服务接管。恢复测试：`python scripts/verify-oem-restore.py` 恢复原服务后再次接管。测试脚本不能当作日常启动程序。

## Logo

assets/LumaDesk.svg 是用户提供的原始 Logo。运行 ./scripts/generate-icons.ps1 可从该源图生成透明 PNG 和多尺寸 ICO，并同步 app/Assets/AppIcon.ico；脚本不重新设计图形。
