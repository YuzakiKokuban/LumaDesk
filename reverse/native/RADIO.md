# Windows 飞行模式与 Fn 输入

2026-10-04，耀世 15 Air / EC 项目 0x1A。本轮用户确认 Fn 锁已生效，而 Fn+F4 没有切换飞行模式。

初次混合 `AcpiTest_EventULong` 采集只出现 B8/BA，没有 A4。独立模式键紫色变蓝时没有 WMI 推送。[混合输入记录](evidence/special-input.jsonl) 包含 `VK_FF / E0 / scan 0x78` 的 Fn 标记及其按下期间的普通 F4；这只能支持输入解析候选，不能证明物理 Fn+F4 已工作。后续用户单独按键并加入低级键盘钩子后，只捕获到 Fn 标记，没有 F4/F24、无线 HID 报告或 A4；本机物理快捷键仍未完成适配。单独 Fn 和普通 F4 均不执行切换，其他机型不启用该 Raw Input 候选。固件 A4 已接入真实切换，重复/并发通知被过滤；Raw Input 不吞掉前台键盘消息。

`AirplaneModeControl` 调用 Windows Radio Management 的桌面 COM 接口。CLSID 为 `581333F6-28DB-41BE-BC7A-FF201F12F3F6`，IID 为 `DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7`。IUnknown 后的方法顺序为 IsRMSupported、GetUIRadioInstances、GetSystemRadioState、SetSystemRadioState、Refresh、OnHardwareSliderChange。系统 radioEnabled=0 表示飞行模式开启，1 表示关闭。

接口布局参考原始开源实现 [SleepToAirPlane](https://git.petertanner.dev/peter/SleepToAirPlane/commit/be89f1940f29ec936fa2d5ab13f0b047b85163fe)。这是未公开的桌面接口，不能视为 Microsoft 稳定的公开 API。每次访问检查支持状态、HRESULT 和读回值；失败记录日志并显示失败提示，不写注册表或猜测某一块无线网卡的状态。只在机耀处已接管 OEM 控制时执行 Fn+F4 切换。

本机使用生产实现完成关闭→开启→关闭，三次 COM 读回一致，最终恢复原状态。`verify-system-osd.ps1 -RoundTripRadio` 的该选项会实际切换无线状态并在 finally 中恢复；CI 默认只测试输入解码，不对测试宿主无线开关进行写入。最终桌面宿主中的物理 Fn+F4 往返、Fn 锁两种状态及其他 Windows 版本仍需人工确认。

原厂样本 AirplaneModeCtrl 使用 UWAirplane.dll 的 SwitchAirplaneMode，并保留 SetAirplaneModeNoDriver 方法。本机未安装可选 vhidmini 驱动，但 Windows COM 切换已经成功，不能仅凭驱动缺失判定键盘不产生事件的原因。没有安装驱动或改写未知 EC 字段。

[独立 Fn+F4 采集](evidence/fn-isolated-input.jsonl) 保留用户确认按键时的低级钩子与 Raw Input 记录；两次操作只见 Fn 标记。用户尚未尝试原厂控制中心运行时的该功能。原厂服务对照尚未执行，本预发布不包含对原厂服务或驱动的新修改。

OSD 订阅 RmSvc 的系统状态注册表变更，初始值和重复值不弹提示。仅观察服务维护的值；Wi-Fi 单独关闭不等同于飞行模式。概念与硬件通知依据 Microsoft [全局无线状态](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/mb-radio-state) 和 [HID 飞行模式控制](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/airplane-mode-radio-management)。
