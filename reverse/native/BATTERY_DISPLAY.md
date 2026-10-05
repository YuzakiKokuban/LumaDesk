# 充电阈值与显示控制修复

2026-10-05，耀世 15 Air / EC 项目 `0x1A`，Windows 内置屏幕 `EDO3C15`。

## 电池养护

用户截图和只读采集均为 `0x7B9 = 0xD0`，实际阈值为 80%。[Linux Uniwill 协议定义](https://github.com/torvalds/linux/blob/master/drivers/platform/x86/uniwill/uniwill-acpi.c) 将此地址的低 7 位定义为百分比，bit 7 为已达到阈值状态。旧实现将整字节 208 当百分比，错误地显示未知；写入验证也可能受固件异步更新状态位影响。

读回现在仅解析低 7 位；0 为固件未初始化的无限制状态，对应 100%，101–127 仍报告无效。没有使用保存的偏好代替实际读数。写入及回滚保留最新状态位，只验证阈值字段；`0x7A6` 保留充电模式字段以外的位。已恢复的四寄存器控制流程和项目限制保持不变。这些位定义属于协议事实，未复制上游驱动代码。

## 内置屏幕亮度

旧 `\\.\LCD` 查询在本机返回 `0x80070032`，WMI 的活动内置屏幕则正常返回 40%。实现改用 [WmiMonitorBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness) 与 [WmiSetBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmisetbrightness-method-in-class-wmimonitorbrightnessmethods)，依 InstanceName 配对读取与写入实例，不改变外接显示器。

WMI 枚举包含 `__PATH`，避免把 `WBEM_FLAG_NONSYSTEM_ONLY` 错当成数据物化选项。Timeout 按 [WMI 数字类型映射](https://learn.microsoft.com/en-us/windows/win32/wmisdk/numbers) 使用 VT_I4，Brightness 使用 VT_UI1。本机提供程序成功时没有输出参数，因此仅在重新读取的实际亮度匹配时接受这种结果。

## 刷新率

刷新率列表限定当前分辨率及色深，避免混入其他显示模式。对未列出的 90/120 Hz，用 [ChangeDisplaySettingsExW / CDS_TEST](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw) 仅测试驱动是否接受；查询不会切换屏幕或写注册表。驱动确认后才允许应用，界面提供 60/90/120 Hz 快捷选择并读回实际刷新率。

[Release 实机往返](evidence/battery-display-hardware-validation.json) 分别检查充电阈值 80→60→80、亮度 40→41→40、刷新率 240→90→120→240，分辨率与色深保持 2560×1600 / 32 bpp。模拟窗口报告另行检查快捷选择、控件保持和窄窗口布局；实机 API 往返不替代长期充电截止、其他屏幕及睡眠恢复验证。
