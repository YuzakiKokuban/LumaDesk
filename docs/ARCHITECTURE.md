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

MUX GUID 为 `{9F33F85C-13CA-4FD1-9C4A-96217722C593}`。优先 OemMagicVariable，兼容 UniWillVariable，保留实际长度和其他字节，仅修改 0x62；APVersion 位于 0x43。Intel / AMD 编码不同，见 [MUX](../reverse/native/MUX.md)。切换前将系统 LastBootUpTime、变量名、原模式和目标模式原子保存到 mux-change.json。应用退出再打开仍保留待重启提示；系统启动标识变化或切回原模式后解除提示。无法取得启动标识时拒绝新的切换；既有记录不确定时保留提示。固件配置不代表当前显示路由已经生效。

NVIDIA 动态加载 `%SystemRoot%\System32\nvml.dll`，只调用查询接口，依据 [NVML 官方文档](https://docs.nvidia.com/deploy/nvml-api/latest/index.html)。失败时保留 Windows 身份信息，测量项为 null。

## C ABI

库名 `jiyaochu_core.dll`。导出为 lumadesk_init、lumadesk_call、lumadesk_drain_events、lumadesk_free、lumadesk_abi_version，ABI 为 1。

新增 `lumadesk_start_oem_hotkeys`，由桌面宿主传入进程生命周期内保持存活的 C 回调。独立线程通过 `ExecNotificationQuery` 订阅 `AcpiTest_EventULong`，没有 WITHIN 轮询间隔；等待推送后通知 UI，接管状态关闭时忽略热键。亮度和热键订阅各自有独立连接状态，初次失败或订阅终止时按 1–30 秒退避重试；重复启动不会创建额外线程。事件等待每 5 秒允许检查重连请求，不重新查询硬件。system.subscriptions 提供连接状态、失败原因和连接次数，system.reconnect_events 请求重连；桌面在睡眠恢复时重建 Windows 系统通知并请求 WMI 重连。

`lumadesk_start_display_brightness` 独立订阅 `WmiMonitorBrightnessEvent`，直接将活动显示器的亮度百分比交给 UI；不依赖 OEM 接管和热键事件，也不定时查询亮度。两个可选订阅失败时分别记录，避免一个提供程序缺失阻断另一个或启动流程。

命令和 JSON 字段为 snake_case。响应为 `{"ok":true,"data":...}` 或 `{"ok":false,"error":"..."}`。UTF-8 返回指针须由 lumadesk_free 释放。get_gpu_mode_info 提供来源、平台、APVersion、模式及支持状态；restart_system 只能由明确操作触发。

Core.cs 管理指针；Backend.cs 将阻塞调用放在线程池；MachineStore 发布轮询快照。读取失败不能以旧配置充当真实固件状态。

MachineStore 只在主窗口可见时读取完整遥测。后台启动、关闭收起和最小化均暂停温度、转速、GPU 等状态轮询。独立模式键没有 WMI 推送，“后台模式键提示”是默认关闭的独立选项；启用且允许性能提示时，ChassisProfileNotifications 每秒仅读取 EC 项目号及档位两个字节；主窗口可见时复用现有快照。关闭选项后等待配置事件，不周期唤醒。Rust 初始化读取配置、后端标识并应用已保存的日志偏好；桌面启动还会检查 OEM 接管标记、执行必要的首次接管，并迁移既有的本程序登录任务。

后台保留托盘和事件订阅：ACPI `AcpiTest_EventULong` 提供 Fn 通知；Windows 低级键盘钩子提供 Caps Lock / Num Lock / Scroll Lock 通知；`RegisterPowerSettingNotification` 接收电源来源变化；Core Audio 回调接收音量和默认通信麦克风的静音变化。音频设备只在订阅建立和默认设备变更时解析，不进行定时扫描。后台收到通知即可显示 OSD，不恢复硬件遥测。Win 键锁使用事件拦截，不依赖 EC 768 状态位。

锁定键释放后延迟读取 Windows 的 toggle bit，不翻转缓存来预测状态。RmSvc 状态变更提供飞行模式开关提示；固件 A4 和项目 0x1A 的 Fn/F4 输入解析接入 Windows 全局无线开关并检查读回。后者仍是候选路径，本机物理 Fn+F4 尚未产生匹配事件，不能报告为适配完成。Fn 锁由固件切换，B8 只显示变更提示，不推测开关值。[输入与接口证据](../reverse/native/RADIO.md) 记录适用范围及未公开 COM 接口的限制。

OSD 默认不透明度为 60%，可保存 20%–100% 的任意整数值；DWM 负责窗口圆角，禁用系统默认边框颜色，避免与卡片边缘叠加。窗口不激活、不进入窗口切换器且允许点击穿透，显示时长结束后隐藏。

主窗口打开时最大化；原始还原位置为当前显示器宽、高各一半并居中。隐藏后仅保留空根控件，卸载页面和导航视觉树，保留导航选择；再次打开重建控件。主窗口收起后至少等待 2 秒，且两次内存回收至少间隔 60 秒；重新打开会取消等待。一次收起仅安排一次延迟清理，没有周期清理。OSD 消失不触发 GC 或工作集释放。工作集清理后的低驻留量不等于私有提交下降。

登录任务迁移通过显式 `migrate_autostart_task` 命令在桌面初始化时执行一次。只处理同用户、Highest / Interactive、单一 Exec、绝对路径指向当前 `LumaDesk.exe` 或同目录旧版 `机耀处.exe` 的 `LumaDesk` 任务。空参数补为 `--background`；旧名称仅接受空参数或已有 `--background`，同时更新可执行路径。以原 CIM 任务对象更新，保留启用状态、触发器、主体、工作目录和其他设置；不存在的任务或自定义参数不被修改。

## 数据

设置保存在 `%APPDATA%\JiYaoChu`。JIYAOCHU_DATA_DIR 可隔离开发数据；无效配置保留 .bad 副本。

未实现的硬件路径返回 Unsupported。保留的文件/事件接口仍需逐项审计，不能因返回成功就宣称对应设备功能完成。

## 设置读回与保存

SettingsResource 保留最后成功的数据，应用设置后的读取不会把控件替换成加载画面。SystemPage 只重读被修改的设置组；LightingPage 等待写入和硬件读回完成再解除忙状态。设置分组采用稳定键，提示保留固定位置，保护滚动位置与控件生命周期。读回失败继续保留已有内容并显示错误；首次读取没有旧数据时才展示加载或错误页。

配置更新在内存锁与系统文件锁内读取最新文件、应用字段修改并写入唯一临时文件，sync_all 后替换。成功后才更新内存，失败保持原内存设置。桌面与 CLI 的不同字段更新不会覆盖彼此。硬件写入已成功、配置持久化失败时保留明确的部分成功提示；OSD 保存成功后才广播新配置。

自动性能档位默认关闭。开启后通过 Windows 电源来源通知应用 AC/DC 规则，不依赖遥测；手动档位保持至下次电源事件或规则修改。托盘提供三档和强冷开关。诊断和设置备份在数据目录 exports 下生成 ZIP；备份不包含 OEM 接管恢复标记，诊断读取缓存状态并标明更新时间，不自动改写设备。boot.log 保留历史并按 2MiB 分段，ui-errors.log 按 1MiB 分段。

显示页合并刷新率、内置屏幕亮度与显卡输出模式，共用一个滚动区域。Windows 枚举显示器与当前分辨率支持的刷新率，写入后重新枚举确认；亮度仅控制 Windows WMI 暴露的内置屏幕，显卡模式仍需明确保存并在重启后生效。趋势图复用 MachineStore 的成功快照，保留最近五分钟、最多 600 个采样；未知值和收起后的间隔断开曲线，不补采、不落盘。

设置恢复先校验 ZIP 路径、文件数量与大小、JSON 结构和版本；新版清单同时校验每个设置文件的 SHA256，预览与应用绑定归档摘要。恢复前备份当前设置。默认仅恢复应用偏好与兼容的预设，不触发硬件自动规则；可显式选择逐项恢复已验证的硬件设置。MUX、自启动、OEM 接管与重启安排不属于恢复范围。文件提交失败回滚已修改文件，失败回滚会单独报告。旧版无清单的备份按受限文件格式迁移，未知新版本拒绝恢复。
