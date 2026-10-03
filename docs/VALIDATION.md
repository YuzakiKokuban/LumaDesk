# 验证记录

2026-10-02，耀世 15 Air / EC 0x1A，Intel，RTX 5060 Laptop GPU。

## 自动检查

### 本次 OSD / 后台更新

后台 OSD 的来源为固件和 Windows 推送事件，独立于 MachineStore 遥测。新增 Caps Lock / Num Lock / Scroll Lock、电源来源、音量与默认通信麦克风静音通知；无状态的 Fn 通知仅显示变更提示。完整 Fn 组合、真实供电插拔和音频设备切换仍需实机验证，下列模拟报告不替代这些物理操作。

- Rust 30 项、EC/MUX 离线 9 项、Clippy 与 Debug 构建通过。
- `verify-win-key.py` 验证解锁→锁定→解锁的按键投递：左右 Win 键锁定后均被拦截，其他键仍到达下游。
- [窗口报告](../reverse/native/evidence/shell-validation.json)：后台启动遥测计数 0，第一次打开后为 4，第一次收起前后均为 4。再次打开并收起后计数 9，锁定键/供电事件和 Fn+F3 两次切换仍保持 9。关闭与最小化收起、托盘双击恢复、OSD 不抢焦点/点击穿透/定时隐藏、彻底退出通过。默认 60% 与自定义 37% 不透明度通过；客户区与外框尺寸一致，实际截图确认白色边线及右侧空隙消失。使用隔离模拟后端、asInvoker 测试宿主。
- [快捷键记录](../reverse/native/evidence/hotkey-validation.json)：用户实机按键确认 Fn+F1=CC，Fn+F3=40 后接 A5。后者去重为一次状态变更。尚未在本轮管理员正式宿主中再次人工确认完整呼出/锁定往返。
- 安装依赖检测的六种隔离场景通过，安装 EXE 与 ZIP 编译及包完整性检查通过。本轮管理员验证的 UAC 被取消，因此新安装器的实际安装/卸载和浏览器跳转尚未实测。

`verify-autostart-migration.py` 使用隔离任务替身执行迁移脚本，检查当前可执行文件与用户身份、旧参数迁移、禁用状态及其他任务元数据保留；不改动本机注册任务。真实旧任务升级和重新登录仍需验证。

`verify-system-osd.ps1` 检查 11 个系统通知的解码、去重与重复按键过滤；本机 2 个默认音频端点的实际 COM 订阅及注销通过。该检查不调整音量或静音状态。CI 对无音频设备的宿主跳过真实音频订阅。

以下为此前版本的验证记录，旧的 EC Win 键寄存器回读不再作为当前锁定实现的依据。

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
