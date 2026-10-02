# 显卡 MUX 配置链

更新：2026-10-02。原版 OpenRevo 0.8.5 的保存链已定位；机耀处完成独立实现和管理员读取，尚未执行实际模式写入、重启及物理路由验证。

## 原版调用链

命令入口 0x14034F990 调用主逻辑 0x140307500。GetFirmwareEnvironmentVariableW / SetFirmwareEnvironmentVariableW 操作以下命名空间：

```text
GUID = {9F33F85C-13CA-4FD1-9C4A-96217722C593}
OemMagicVariable / UniWillVariable
APVersion = byte[0x43]
GPU mode = byte[0x62]
```

先 OemMagicVariable，失败再 UniWillVariable。读取容量 512 字节，按返回实际长度写回；替换模式字节，其他内容保持不变。OemDgpuPresent 是另一个存在性变量，不等于当前模式。

| 平台 | 核显 | 独显 | 混合 |
| --- | --- | --- | --- |
| Intel | 1 | 2 | 4 |
| AMD | 2 | 1 | 0 |

这些编码与官方 .NET 常量相符。使用 OemMagicVariable 或 APVersion≥25 时写 OemMagicDoor=1。原版 Door 失败仅警告后继续写正式变量；机耀处当前在 Door 写失败时返回错误，避免静默继续。

原版保存后可调用 0x1403072A0 执行 shutdown /r /t 0，再更新配置或发事件。机耀处将保存与重启拆开，确认后才安排 30 秒延迟重启。通知不证明物理 MUX 已切换。

## 官方 DLL 对照

UEFI_Firmware.dll ReadUefi 0x180002190 使用相同 GUID、变量和 512 字节容量。WriteUefi 0x180002330 读取后修改指定区间，并保持原长度写回；ReadUefiGPU 0x1800022C0 读 OemDgpuPresent。

官方元数据包含 GetFwVarsGPU、SetFwVarsForModeSwitchChange 和模式常量，但受保护方法体未恢复。当前 DLL 导出 6 项；C# 声明 ReadSwitchUefi/WriteSwitchUefi 不证明当前 DLL 提供这些函数。不能宣称官方业务层与原版 0x62/Door 分支完全等同。

## 本机状态

[管理员只读证据](evidence/mux-read-admin.json)：Intel、OemMagicVariable=180 字节、APVersion=25、模式字节=4、OemDgpuPresent=1；UniWillVariable 缺失错误 203。早先非管理员错误 1300/1314 来自特权不足，管理员已成功读取。

[新后端实测](evidence/hardware-validation.json)返回 configured_mode=hybrid、supported=true。本机 1A/AP25 开放核显选项；其他机型需要分别确认。支持标志表示实现具备保存路径，不代表实机切换已经验证。

## 实现与边界

Rust `src/core/driver/uefi.rs` 启用 SeSystemEnvironmentPrivilege，检查 NOT_ALL_ASSIGNED，识别平台、长度及已知编码。保存仅修改模式字节，按原长度写入，再读取验证。进程内锁串行化访问，不能阻止其他程序修改固件变量。

页面显示固件保存配置，选择卡片只修改草稿。点击保存才写固件；重启另行确认。pending_reboot 根据本进程初始值判断，跨应用重启不可靠。真实生效状态需要重启后检查内屏所属显示适配器与设备行为。

## 可复查证据

- [关键汇编](evidence/key-disassembly/)
- [官方接口字符串](evidence/oem-uefi-strings.json)
- [官方常量](evidence/oem-metadata-constants.json)
- [离线编码与补丁](mux_protocol.py)、[离线测试](test_mux_protocol.py)
- [只读脚本](probe_mux.py)

离线测试与 Rust 测试验证模式编码、边界和其他字节保留，不代表已成功改变物理显示路由。
