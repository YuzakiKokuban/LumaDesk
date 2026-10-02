# 验证记录

2026-10-02，耀世 15 Air / EC 0x1A，Intel，RTX 5060 Laptop GPU。

## 自动检查

Rust 单元与 FFI 测试 30 项，Python EC/MUX 离线测试 9 项。Clippy 开启警告即失败；WinUI 构建与发布包验证纳入 [CI](../.github/workflows/ci.yml)。发布包检查包含 ZIP 完整性、七种尺寸图标、管理员清单、CLI、FFI，以及不内置系统运行时。

0.2.0-beta.1 安装版在本机完成依赖检查、空目录安装、已安装 CLI、界面启动和卸载。卸载恢复 OEM 服务后再次接管，最终为 Disabled / Stopped；个人设置保留。CI 也执行安装与卸载，结果见 [构建记录](https://github.com/YuzakiKokuban/LumaDesk/actions/runs/36983030136)。体积与内存采样见 [性能记录](PERFORMANCE.md)。

## 实机控制

[控制报告](../reverse/native/evidence/control-validation.json) 保存寄存器快照、API 返回值和恢复结果：

- OEM 办公、均衡、狂暴三档及 Windows 三种模式设置后回读一致。
- 键盘白光关闭、亮度 1–4，再依次切换八种颜色；第三轮每档停留 6 秒。
- 每次灯光调整后交叉切换 OEM 三档，亮度控制字段保持不变。
- 充电上限 60%、80%、100% 设置后回读一致。
- Win 键锁定开关设置与回读一致。
- 强冷开启后 CPU/GPU 风扇约 5659/5411 RPM，关闭后约 2351/2339 RPM。
- 电池满电且未充电时，供电状态仍正确识别为连接电源。
- 检查结束恢复原 EC 与 Windows 模式，`ec_restored=true`，无恢复错误。

[OEM 服务往返](../reverse/native/evidence/oem-roundtrip.json)：恢复原 Auto/Stopped，再次接管后 Disabled/Stopped。该机器未匹配到官方计划任务，非空任务集合的恢复尚未实测。

MUX 当前混合模式读取成功。本轮没有保存其他 MUX 模式或重启。充电截止、Win 键实际行为、键盘肉眼亮度与颜色变化仍需观察；寄存器回读单独记录，不代替观察结论。

[管理员自启动](../reverse/native/evidence/startup-validation.json)：登录任务注册为 Highest，手动运行任务成功启动应用并保持运行，检查后恢复原任务状态。尚未注销或重新登录。

## 界面

键盘页面使用紧凑五档按钮与统一颜色按钮，成功写入后重新读取硬件状态。布局约束内容宽度并自动换行，侧栏使用原生展开/收起布局。当前窗口诊断 viewport=946、content=930、horizontalOverflow=0。

原 Logo 已恢复；PNG/ICO 增加白色圆角矩形底衬。管理员窗口的自动输入没有取得可靠结果，全部页面在窄窗口下的视觉检查尚未完成。
