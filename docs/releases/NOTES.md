本版本面向机械革命耀世 15 Air，提供 Windows x64 安装器、便携 ZIP 和 SHA256。需要共享 .NET 10、Windows App SDK Runtime 与原厂 UWACPIDriver；具体安装依赖见 RUNTIMES.md。

设置应用后保留页面控件和滚动位置；通知支持自动重连，MUX 待重启状态持久化。包含配置并发保存保护、诊断导出、设置备份、可选 AC/DC 自动档位及托盘快捷操作。

自动检查使用隔离模拟后端，覆盖设置体验、窗口与 OSD、协议、安装及卸载。支持机型和实际验证范围见 docs/VALIDATION.md；完整 MUX 重启、充电截止、背光效果、Fn 组合、睡眠恢复和重新登录自启动仍需实际确认。

升级前可以从系统设置导出配置备份。每次发布前维护此说明；版本专用的 docs/releases/<version>.md 存在时优先使用。
