# 机耀处 · LumaDesk

面向机械革命笔记本的 Windows 控制中心，优先适配 **耀世 15 Air / EC 项目 0x1A**。C# WinUI 3 界面、Rust 硬件后端，当前版本 `0.1.0`。

项目地址：[YuzakiKokuban/LumaDesk](https://github.com/YuzakiKokuban/LumaDesk)。源码与提交历史独立维护；旧版图标、赞助页面和截图已移除。协议来源与实现边界见 [来源说明](docs/PROVENANCE.md)。

## 使用

1. 解压完整的 `LumaDesk-0.1.0-win-x64.zip`，不要单独移动 EXE。
2. 启动 `机耀处.exe` 并接受管理员权限请求。
3. 首次启动自动接管官方控制中心。系统页可恢复原服务与计划任务状态；恢复后取消自动接管。
4. 电源页主卡片调整机械革命机身档位；下方 Windows 电源模式是独立设置。
5. 显卡模式先选择、再保存；重启是单独操作。

依赖本机已安装的 `UWACPIDriver`，不分发或自动替换驱动。便携包包含 .NET / Windows App SDK 运行组件。

## 功能与验证

| 功能 | 当前状态 |
| --- | --- |
| 机械革命办公、均衡、狂暴档位 | EC 写入和回读通过；功耗默认值从本机固件读取 |
| Windows 11 能效、平衡、性能 | 独立 API，三种模式写入和回读通过 |
| 键盘开关、五档亮度、单色颜色 | EC RGB 写入和回读通过；动态、单键、分区效果仍待适配 |
| 双风扇转速、强冷 | 实机读取；强冷位切换和恢复通过 |
| NVIDIA 温度、频率、负载、显存 | NVML 实机读取，失败项显示“—” |
| CPU 负载、频率、热区温度 | Windows / WMI / ACPI；热区温度不等同核心温度 |
| 充电上限 | 50–100% 写入已实现，60/80/100% 预设；实际充电停止行为待测 |
| MUX | 管理员读取成功；保存代码已实现，重启后显示路由待验证 |
| Win 键锁定 | EC 写入与回读；实际按键行为待测 |
| 官方服务接管 / 恢复 | 实机往返验证，备份原启动方式与任务启用状态 |
| 管理员自启动 | 最高权限登录计划任务；登录启动行为待验证 |
| 自定义风扇、GPU 功耗、Fn 锁、USB 供电、BIOS 菜单、独立 OSD | 尚未完成，不显示可操作的无效开关 |

寄存器读回不等于整机功耗、灯光视觉效果或重启后路由验证。详细范围见 [功能矩阵](docs/FEATURES.md) 和 [验证记录](docs/VALIDATION.md)。

## 界面

页面宽度跟随窗口与侧栏；卡片自动换行，选择态使用统一边框与文字标记。键盘页使用紧凑的亮度选择与色块，系统页只提供已接入开关。最后一轮布局修改已编译，仍需实机检查不同窗口尺寸。

## 配置与开发

数据目录 `%APPDATA%\JiYaoChu`。首次复制旧 `%APPDATA%\OpenRevo` 配置，原目录保留。`oem_takeover.json` 是官方服务恢复备份，不要在接管状态下删除。

```powershell
cargo build
cargo test
 dotnet build app/JiYaoChu.csproj -c Debug
./scripts/build.ps1
```

构建说明：[BUILD](docs/BUILD.md)。CLI 与界面共用后端：

```powershell
.\jiyaochu-ctl.exe get_hardware_status
.\jiyaochu-ctl.exe get_gpu_mode_info
.\jiyaochu-ctl.exe get_power_settings
.\jiyaochu-ctl.exe --commands
```

[架构](docs/ARCHITECTURE.md) · [机型配置](docs/models_customization_guide.md) · [后续工作](docs/ROADMAP.md) · [English](README_en.md)

代码采用 [MIT License](LICENSE)，保留必要的 [第三方许可说明](THIRD_PARTY_NOTICES.md)。官方二进制、原版 EXE、完整反编译输出和个人配置不进入仓库或发布包。
