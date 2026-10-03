# 运行组件

机耀处的安装版和便携版共用系统运行时。首次使用前，安装以下 **x64** 组件；已经安装的组件无需重复安装。

| 组件 | 版本 | 官方下载 |
| --- | --- | --- |
| .NET Desktop Runtime | 10.0.x，建议最新补丁 | [.NET 10 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| Windows App SDK Runtime | 2.5.1 或更新的 2.x 稳定版 | [2.5.1 x64 安装程序](https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-x64.exe) · [官方下载页](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) |

.NET Desktop Runtime 包含应用需要的 .NET Runtime，普通用户无需安装开发 SDK。Windows App SDK Runtime 是 WinUI 的运行组件，Windows 11 的系统版本本身不能代替它。

安装程序始终要求管理员权限。点击“安装”时分别检测 x64 .NET 10 Runtime 和 Windows App SDK Runtime；缺失哪个组件就打开哪个官方下载页，完成依赖安装后可在原安装窗口重新点击“安装”。静默安装只返回缺失组件信息，不打开浏览器。

安装组件后，打开 `LumaDesk.exe` 并允许管理员权限。机械革命的 UWACPIDriver 仍由原厂控制中心安装，机耀处不替换驱动。

安装程序在安装前检查依赖，缺少组件时给出下载地址。便携版使用相同依赖。
