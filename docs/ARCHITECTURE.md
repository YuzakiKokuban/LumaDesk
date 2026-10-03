# 架构与接口

## 组成

app 是 C# WinUI 3 + Reactor 前端。src/core 提供 Rust API、HAL、配置和硬件访问；src/ffi.rs 导出 C ABI。CLI 和界面共用命令分发。

```text
WinUI / jiyaochu-ctl
       ↓ JSON
      Api
       ↓ HardwareHal
Windows API / EC / UEFI / NVML
```

Windows 使用真实后端，模拟后端用于测试。`JIYAOCHU_FORCE_MOCK=1` 用于开发测试，模拟值不能作为实机证据。

## 硬件访问

EC 设备为 `\\.\ACPIDriver`，读取 IOCTL 0x9C40A488，写入 0x9C40A48C，参数为小端 DWORD。进程内事务锁覆盖读改写，检查返回长度和读回。它不阻止官方服务或固件并发访问，不是驱动层原子事务。

MUX GUID 为 `{9F33F85C-13CA-4FD1-9C4A-96217722C593}`。优先 OemMagicVariable，兼容 UniWillVariable，保留实际长度和其他字节，仅修改 0x62；APVersion 位于 0x43。Intel / AMD 编码不同，见 [MUX](../reverse/native/MUX.md)。待重启状态依据本进程首次读值，应用重启后不能可靠推断上次是否已完成系统重启。

NVIDIA 动态加载 `%SystemRoot%\System32\nvml.dll`，只调用查询接口，依据 [NVML 官方文档](https://docs.nvidia.com/deploy/nvml-api/latest/index.html)。失败时保留 Windows 身份信息，测量项为 null。

## C ABI

库名 `jiyaochu_core.dll`。导出为 lumadesk_init、lumadesk_call、lumadesk_drain_events、lumadesk_free、lumadesk_abi_version，ABI 为 1。

新增 `lumadesk_start_oem_hotkeys`，由桌面宿主传入进程生命周期内保持存活的 C 回调。独立线程通过 `ExecNotificationQuery` 订阅 `AcpiTest_EventULong`，没有 WITHIN 轮询间隔；阻塞等待推送后通知 UI，接管状态关闭时忽略热键。

命令和 JSON 字段为 snake_case。响应为 `{"ok":true,"data":...}` 或 `{"ok":false,"error":"..."}`。UTF-8 返回指针须由 lumadesk_free 释放。get_gpu_mode_info 提供来源、平台、APVersion、模式及支持状态；restart_system 只能由明确操作触发。

Core.cs 管理指针；Backend.cs 将阻塞调用放在线程池；MachineStore 发布轮询快照。读取失败不能以旧配置充当真实固件状态。

MachineStore 只在主窗口可见时读取遥测。后台启动、关闭收起和最小化均暂停 CPU / GPU / EC 等状态轮询。Rust 初始化只读取配置和后端标识；桌面启动还会检查 OEM 接管标记、执行必要的首次接管，并迁移既有的本程序登录任务。

后台保留托盘和事件订阅：ACPI `AcpiTest_EventULong` 提供 Fn 通知；Windows 低级键盘钩子提供 Caps Lock / Num Lock / Scroll Lock 通知；`RegisterPowerSettingNotification` 接收电源来源变化；Core Audio 回调接收音量和默认通信麦克风的静音变化。音频设备只在订阅建立和默认设备变更时解析，不进行定时扫描。后台收到通知即可显示 OSD，不恢复硬件遥测。Win 键锁使用事件拦截，不依赖 EC 768 状态位。

OSD 默认不透明度为 60%，可保存 20%–100% 的任意整数值；DWM 负责窗口圆角，禁用系统默认边框颜色，避免与卡片边缘叠加。窗口不激活、不进入窗口切换器且允许点击穿透，显示时长结束后隐藏。

登录任务迁移通过显式 `migrate_autostart_task` 命令在桌面初始化时执行一次。只处理同用户、Highest / Interactive、单一 Exec、绝对路径与当前机耀处.exe 相同且无旧参数的 `LumaDesk` 任务，将参数补为 `--background`。以原 CIM 任务对象更新，保留启用状态、触发器、主体、工作目录和其他设置；不存在的任务或自定义参数不被修改。

## 数据

设置保存在 `%APPDATA%\JiYaoChu`。JIYAOCHU_DATA_DIR 可隔离开发数据；无效配置保留 .bad 副本。

未实现的硬件路径返回 Unsupported。保留的文件/事件接口仍需逐项审计，不能因返回成功就宣称对应设备功能完成。
