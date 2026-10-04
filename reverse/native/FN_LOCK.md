# Fn 锁与麦克风状态

2026-10-04，耀世 15 Air / EC 项目 `0x1A`。

## Fn 锁只读协议

Linux 上游 Uniwill 驱动将 `EC_ADDR_BIOS_OEM` 定义为 `0x074E`，`FN_LOCK_STATUS` 为 bit 4；其读取函数依据该位返回锁定状态。[核对版本](https://github.com/torvalds/linux/blob/e6137bbef0fe23feaf19e4714892457853064eb6/drivers/platform/x86/uniwill/uniwill-acpi.c)。这里只使用寄存器地址与位定义这些协议事实，未复制驱动实现。

本机用户两次按 Fn+Esc 的[只读采集](evidence/fn-lock-live-state.jsonl) 中，`values` 顺序为项目号 `0x740`、状态 `0x74E`、候选开关 `0x7A4`。项目始终为 26，状态为 `0 → 16 → 0`，候选开关保持 0。[同期原厂通知](evidence/fn-microphone-live-wmi.jsonl) 的前两条为 B8。初始时间早于按键，后两次变化与通知对应。

`get_fn_lock` 在同一 ACPI 事务中先验证项目为 `0x1A`，再读取 `0x74E & 0x10`。其他项目返回不支持；不写 EC，也不采用保存的偏好或按键次数推算状态。B8 触发一次读回，OSD 显示“已锁定/已解锁”；失败显示“状态未知”。旧 `set_fn_lock` 仍未实现物理写入。

## Fn+F2 默认录音设备

同期用户两次 Fn+F2 发出 B7，但 Windows 原有默认通信录音端点的静音值一直为 false。因此通知只证明快捷键请求，不能证明已经静音。新增处理在确认项目 `0x1A` 且 LumaDesk 接管 OEM 时，通过 Windows `IAudioEndpointVolume` 切换 **默认录音设备（eCapture/eConsole）**，使用 `GetMute` 读回结果后显示 OSD。OEM 未接管时只读取，不执行第二次切换。

音频状态订阅使用同一个默认录音角色；Windows 或其他应用改变该设备的静音状态时也更新 OSD。无设备、协议不支持或接口失败显示“状态未知”，不假定切换成功；并发/伴随通知去重，停止后不发布旧请求。只控制默认录音端点，不代表所有麦克风、系统隐私权限或应用内部静音状态。

接口依据：[GetMute](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-getmute)、[SetMute](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmute)。[本机接口往返](evidence/fn-microphone-system-validation.json) 记录未静音→静音→未静音并恢复原值；这与新安装版物理 Fn+F2 的完整事件链验证分别记录。
