<img src="assets/LumaDesk.png" width="96" alt="LumaDesk Logo">

# 机耀处 · LumaDesk

[![构建](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml)

机械革命笔记本控制中心，当前适配 **耀世 15 Air**。集中管理性能档位、键盘背光、充电上限与显卡输出模式，提供 CPU、显卡和风扇实时状态。

## 功能

- **性能与散热**：办公、均衡、狂暴三档，一键强冷与双风扇转速。
- **键盘背光**：开关、四档亮度和八种单色预设。
- **电池养护**：60%、80%、100% 充电上限，电量与供电状态。
- **显卡模式**：混合输出、独显直连、核显输出，保存后重启生效。
- **系统设置**：Windows 电源模式与电源计划、Win 键锁定、管理员自启动、官方控制中心接管与恢复。

完整适配情况见 [功能说明](docs/FEATURES.md)，本机测试结果见 [验证记录](docs/VALIDATION.md)。

## 安装与使用

1. 从 [Releases](https://github.com/YuzakiKokuban/LumaDesk/releases) 下载 `Setup.exe` 安装版，或将 Windows x64 便携 ZIP 解压到固定目录。
2. 打开 `机耀处.exe`，允许管理员权限。
3. 在左侧选择页面，调整对应设置。

运行环境为 Windows 11 x64，需要 .NET 10 与 Windows App SDK Runtime 2.5.1，以及原厂控制中心安装的 UWACPIDriver。[运行组件下载](https://github.com/YuzakiKokuban/LumaDesk/blob/main/docs/RUNTIMES.md)。

首次启动会接管官方控制中心。需要切回官方软件时，在“系统设置”中选择“还原官方控制中心”。显卡模式保存后需要重启，其他常用设置立即生效。

启用自启动后，请保留程序所在目录。配置与日志位于 `%APPDATA%\JiYaoChu`。安装版提供开始菜单快捷方式；卸载时还原官方控制中心，保留个人设置。

## 项目资料

[构建与发布](docs/BUILD.md) · [架构](docs/ARCHITECTURE.md) · [协议资料](reverse/native/REPORT.md) · [English](README_en.md)

[MIT License](LICENSE) · [第三方许可](THIRD_PARTY_NOTICES.md)
