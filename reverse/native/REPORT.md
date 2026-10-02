# 官方接口与 OpenRevo 协议分析

更新：2026-10-02。用于机耀处 · LumaDesk 的独立实现。分析对象包括原版 OpenRevo 0.8.5、官方 DLL、安装的 UniWill 驱动与受保护 .NET 元数据；不是完整源码恢复。

## 结论与实机

EC 使用 `\\.\ACPIDriver`。早期将原版入口描述为 ACPIH 的结论已修正；ACPIH 仅保留兼容探测。管理员读取 MUX 已成功：OemMagicVariable 180 字节、APVersion 25、Intel 混合模式 4。强冷寄存器完成 0x10→0x50→0x10 的读回验证，恢复原状态。

原版 MUX 调用 Windows 固件 API，更改 UEFI 变量 0x62，不是普通 EC GPU 功耗寄存器。详见 [MUX](MUX.md)。官方业务方法体受保护，当前无法宣称所有分支与原版等同。

## 样本

| 样本 | SHA256 |
| --- | --- |
| OpenRevo 0.8.5 | 3e3b9d2297e36a0361d56c0f6357afdd9ebc8b201b943546c5cf8002cfabda09 |
| UWACPIDriver.sys | 6e360ee4a0c42b2a4eaf46208c1592c87f8cd71aca2a6c52831a2ad356e61235 |
| ACPIDriverDll.dll | 97d7115943600c2a09951440859f9bd75fd0d8bff9db49c296c868b49df8c8c6 |
| UEFI_Firmware.dll | 4cfe064827dad6da7bf6fb9fc07dc00b4032f1881331d59bfa8d5937a6d534be |

哈希以 [原始清单](evidence/) 为复查依据；样本和反编译输出不分发。官方安装来源为本机 OEM 控制中心目录。

## EC 封包

| 操作 | IOCTL | 输入 | 输出 | OpenRevo 入口 |
| --- | --- | --- | --- | --- |
| 读 | 0x9C40A488 | DWORD 地址，4 字节小端 | DWORD，取低字节 | 0x14030C030 |
| 写 | 0x9C40A48C | DWORD 地址 + DWORD 值，8 字节 | DWORD，4 字节 | 0x14030C6E0 |

原版使用 u16 地址和 u8 数值，扩展成 DWORD。设备为读写打开、共享读写。官方 DLL ReadEC/WriteEC 位于 0x180003410/0x1800034F0，代码一致但传入 4 MiB 缓冲区；重建无需复制这个尺寸。

驱动分发入口 0x140002870，读取处理 0x140002130，写入 0x1400025B8；向下层发送 ACPI IOCTL 0x32C004 调用 ECRR/ECRW。分发末尾有路径将状态改为成功，所以 DeviceIoControl 成功不能单独证明 ACPI 成功。实现检查返回长度、读回值；读回仍不是设备行为证明。

批量函数 0x14030E4E0/0x14030E890 复用句柄逐项调用，不是驱动原子事务。原版批量读失败可能映射为 0，不能把所有零当测量结果。

## 官方 DLL 接口

| 导出 | IOCTL |
| --- | --- |
| ReadCMOS / WriteCMOS | 0x9C40A480 / 0x9C40A484 |
| ReadEC / WriteEC | 0x9C40A488 / 0x9C40A48C |
| ReadMEMB / WriteMEMB | 0x9C40A490 / 0x9C40A498 |
| ReadPCI / WritePCI | 0x9C40A4A0 / 0x9C40A4A4 |
| ReadIO / WriteIO | 0x9C40A4C0 / 0x9C40A4C4 |
| ReadIndexIO / WriteIndexIO | 0x9C40A4C8 / 0x9C40A4CC |
| TempRead1 / 2 / 3 | 0x9C40A4D0 / 0x9C40A4D4 / 0x9C40A4D8 |
| TempWrite1 / 2 / 3 | 0x9C40A4DC / 0x9C40A4E0 / 0x9C40A4E4 |
| SMAPCTable | 0x9C40A500 |

共 19 个导出映射，[详细表](evidence/oem-ioctl-table.json)。驱动另有 494/49C/504，共 22 个分支，不等于全部分支已恢复。

## 地址证据

| 用途 | EC 地址 | 说明 |
| --- | --- | --- |
| 项目代号 | 740 | 本机 1A |
| CPU 风扇 RPM | 464 高 / 465 低 | 大端组合 |
| GPU 风扇 RPM | 46C 高 / 46B 低 | 注意低字节地址顺序 |
| 默认 Gaming PL | 730/731/732 | 本机 55/55/120 |
| 默认 Office PL | 734/735/736 | 本机 45/45/120 |
| 自定义 PL | 783/784/785 | 提交/生效链未验证 |
| 温度目标 | 786 | 算法未完整恢复 |
| 风扇渐变 | 787 | 禁用 81；启用 80 OR 时间单位 |
| 强冷 | 751 bit 6 | 本机保留位写入与恢复通过 |
| 独立风扇 | 7C5 bit 7、7C6 提交 | 提交时序待确认 |
| GPU 功耗 | 743/744/746 | 不是 MUX |
| 电池上下限 | 7B9/7D0 | 50–100%，下限=上限−5 |
| 电池触发、模式 | 770/7A6 | 模式保留 bit 3，尚未改限值实测 |
| Win 键锁 | 768 bit 0 | 实现读改写，键盘行为待测 |
| BIOS 高级相关 | 74E bit 4 | 单个位不足以证明完整菜单开关 |
| 自定义模式相关 | 706 OR 40 | 原版 helper 0x14030C3A0 |

官方 F00/F10/F20/F30/F40/F50 表与 F5D/E/F 控制来自元数据，本机读到 0，不能据此开放风扇曲线写入。完整地址提示与调用点在 evidence JSON；提示不等于已验证功能。

## 证据与复现

官方反编译目录有 451 文件、4564 个 Runtime exception 占位、2918 条数字常量；不能称为已恢复业务源码。关键汇编、样本哈希和 JSON 表保存在 [evidence](evidence/)。

[管理员硬件验证](evidence/hardware-validation.json)记录双风扇、NVIDIA、固件和强冷恢复。[管理员 MUX 读取](evidence/mux-read-admin.json)补充早先非管理员的 1300/1314 错误。本轮没有执行 MUX 写入或重启；OEM 服务接管与恢复、基础性能档位及 RGB 已完成可恢复写入验证，见 [性能](PERFORMANCE.md)、[RGB](RGB.md)、[控制验证](evidence/control-validation.json) 与 [OEM 恢复](evidence/oem-roundtrip.json)。

```powershell
python -m unittest discover -s reverse/native -p 'test_*.py'
python scripts/verify-hardware.py --library target/debug/jiyaochu_core.dll --output artifacts/hardware-validation.json
```

离线测试验证封包、边界、编码和缓冲区保留；管理员脚本临时改强冷位并恢复。硬件行为验证与协议测试分别记录，见 [验证文档](../../docs/VALIDATION.md)。
