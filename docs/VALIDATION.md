# 验证记录

日期：2026-10-02，北京时间。耀世 15 Air / EC 0x1A，Intel，RTX 5060 Laptop GPU。公开证据不包含设备序列号。

## 自动检查

Rust 单元与 FFI 测试 26 项通过；Python EC/MUX 离线测试 9 项通过。.NET Debug 编译通过，0 警告、0 错误。模拟测试不操作硬件。

## 实机可恢复检查

[控制验证](../reverse/native/evidence/control-validation.json)：

- OEM 三个档位请求与回读一致，751 分别为 A0、00、10。
- 办公默认 PL1/PL2 为 45/45，游戏默认为 55/55，PL4 为 120。
- 性能增强档位由固件恢复部分 PL 控制字节为 0，不能据此声称手动功耗值持续生效。
- Windows 三个电源模式 API 写入后回读一致。
- 红、绿、蓝、白色与亮度 1–4 回读一致；关闭后亮度为 0，颜色字节由固件恢复默认色。
- 测试结束恢复原 EC 字节与原 Windows 模式；报告 ec_restored=true。

[OEM 恢复验证](../reverse/native/evidence/oem-roundtrip.json)：原状态 Auto/Stopped，恢复后保持 Auto/Stopped，再次接管后 Disabled/Stopped。当前接管按用户要求保留。该机器没有匹配到需要修改的官方计划任务，计划任务恢复逻辑尚未在非空任务集合实测。

[先前硬件验证](../reverse/native/evidence/hardware-validation.json)：MUX Intel/AP25/hybrid，强冷 10→50→10，双风扇读取成功。NVML 取得温度、负载与显存；异常频率不作为有效读数。

## UI 与发布

旧 Release 窗口曾因缺主 PRI 无法启动，已加入发布复制目标并成功启动修正版本。此次将根布局改为受约束 Grid，原生 NavigationView、按钮和 ScrollViewer 内容拉伸，页面随可用宽度换行；重做键盘页与性能卡片。

用户此前按 Esc 停止自动窗口检查，此后未继续自动操作窗口。新版 Release 已成功启动；[初始布局诊断](../reverse/native/evidence/layout-initial.json) 记录 viewport=1059、content=1043、horizontalOverflow=0。其他页面、窄窗口和侧栏折叠仍需视觉复核。

## 尚未验证

没有切换 MUX 或重启。实际充电停止、Win 键行为、实际背光色彩与风扇长期响应尚待确认。基本 EC 档位不代表已经恢复官方全部性能策略或 NVAPI 调校。
