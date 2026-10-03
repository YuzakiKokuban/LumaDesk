# 协议证据范围

仅保留实现和离线测试需要的寄存器、IOCTL、MUX 常量及关键汇编。常量 JSON 是从本地完整采集中筛选的记录，值和出处不变。原始 EXE、驱动、反编译目录、无关 UI 字符串和完整汇编采集不分发。

control-validation.json 是可恢复 EC / Windows 模式与 RGB 验证；oem-roundtrip.json 是服务恢复验证。layout-initial.json 只证明新版初始页面没有横向溢出，不代表所有尺寸和页面已检查。

settings-release-validation.json 记录模拟设置读回、原生控件/滚动位置保留、200% 缩放窄窗口检查和内存采样；notification-recovery-validation.json 记录本机被动 WMI 订阅和重连。它们不证明实际 MUX 重启、充电截止或完整睡眠/登录往返。

settings-final-validation.json 是增加通知状态与重连入口后的最终界面回归，关闭截图和长时间空闲采样，其余设置与窗口检查保持启用。
