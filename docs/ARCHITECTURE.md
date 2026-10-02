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

命令和 JSON 字段为 snake_case。响应为 `{"ok":true,"data":...}` 或 `{"ok":false,"error":"..."}`。UTF-8 返回指针须由 lumadesk_free 释放。get_gpu_mode_info 提供来源、平台、APVersion、模式及支持状态；restart_system 只能由明确操作触发。

Core.cs 管理指针；Backend.cs 将阻塞调用放在线程池；MachineStore 发布轮询快照。读取失败不能以旧配置充当真实固件状态。

## 数据

`%APPDATA%\JiYaoChu` 首次复制旧 OpenRevo 文件，跳过链接、不覆盖目标文件，完成后写迁移标记。JIYAOCHU_DATA_DIR 隔离开发数据。无效配置保留 .bad 副本。

未实现的硬件路径返回 Unsupported。保留的文件/事件接口仍需逐项审计，不能因返回成功就宣称对应设备功能完成。
