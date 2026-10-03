<img src="assets/LumaDesk.png" width="96" alt="LumaDesk Logo">

# 机耀处

[![构建](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/YuzakiKokuban/LumaDesk/actions/workflows/ci.yml)
[![许可证](https://img.shields.io/badge/license-MPL--2.0-blue)](LICENSE)

机耀处（LumaDesk）是面向机械革命笔记本的 Windows 控制中心，采用 WinUI 3 界面与 Rust 硬件后端。目前适配 **耀世 15 Air（EC 项目 0x1A）**。

[English](README_en.md)

## 主要功能

- **性能与散热**：办公、均衡、狂暴档位，一键强冷及 CPU、GPU、风扇状态监测。
- **键盘背光**：背光开关、四档亮度、八种单色预设。
- **电源管理**：60%、80%、100% 充电上限，Windows 电源模式与电源计划；可选按供电方式自动切换性能档位。
- **显卡模式**：混合输出、独显直连与核显输出设置，重启后生效。
- **桌面集成**：托盘运行与快捷调节、管理员自启动、Win 键锁定、热键与系统状态 OSD。
- **诊断与备份**：导出版本、通知状态、近期日志和设置备份。
- **官方软件管理**：接管与恢复官方控制中心。

硬件支持与验证范围见 [功能说明](docs/FEATURES.md) 和 [验证记录](docs/VALIDATION.md)。显卡模式尚未完成切换重启验证；背光实际效果与充电截止行为仍待观察。

## 安装与使用

需要 Windows 11 x64、.NET 10、Windows App SDK Runtime 2.5.1，以及原厂控制中心安装的 UWACPIDriver。运行组件见 [依赖说明](docs/RUNTIMES.md)。

1. 从 [Releases](https://github.com/YuzakiKokuban/LumaDesk/releases) 下载安装包，或将便携 ZIP 解压到固定目录。
2. 运行 `LumaDesk.exe`，允许管理员权限。
3. 首次启动接管官方控制中心；需要切回时，在“系统设置”中选择“还原官方控制中心”。

关闭或最小化窗口后程序驻留托盘，双击托盘图标打开，右键退出。配置与日志保存在 `%APPDATA%\JiYaoChu`。

## 开发与文档

- [构建与发布](docs/BUILD.md)
- [项目架构](docs/ARCHITECTURE.md)
- [硬件协议分析](reverse/native/REPORT.md)

## 许可证

本项目采用 [Mozilla Public License 2.0](LICENSE)。
