# 机型配置格式

路径：`%APPDATA%\JiYaoChu\models.json`。这是预设数据，不是 EC 地址配置，不会自动开启未实现功能。

## 结构

顶层数组，mine=true 的当前机型整理到第一项；不要标记多个当前机型。未知字段在读写时保留。

```json
[
  {
    "mine": true,
    "model_id": "yaoshi_15_air",
    "display_name": "MECHREVO YAOSHI Series",
    "alias": "耀世 15 Air",
    "factory_specs": {
      "office": { "pl1": 45, "pl2": 45, "pl4": 120, "temp_offset": 0 },
      "balanced": { "pl1": 45, "pl2": 45, "pl4": 120, "temp_offset": 0 },
      "turbo": { "pl1": 55, "pl2": 55, "pl4": 120, "temp_offset": 0 }
    },
    "presets": {
      "office": { "pl1": 45, "pl2": 45, "pl4": 120, "temp_offset": 0 },
      "balanced": { "pl1": 45, "pl2": 45, "pl4": 120, "temp_offset": 0 },
      "turbo": { "pl1": 55, "pl2": 55, "pl4": 120, "temp_offset": 0 }
    },
    "base_tgp": 0,
    "max_db": 0
  }
]
```

这是格式示例。45/45/120 和 55/55/120 来自本机部分默认 EC 读取；balanced 和温度偏移未独立验证，不是完整官方档位。GPU 数值 0 表示示例未提供已验证值。编辑这些字段不能让硬件功耗控制自动实现。

## 字段

| 字段 | 含义 |
| --- | --- |
| model_id | 内部机型标识 |
| display_name / alias | 固件名称 / 用户显示名称 |
| factory_specs | 参考出厂数据，导入值不构成实测 |
| presets | 用户预设 |
| pl1/pl2/pl4 | 功耗数据，W |
| temp_offset | 偏移格式约定目标 100−offset，固件算法待验证 |
| base_tgp/max_db | 基础 GPU 功耗 / Dynamic Boost，W |

解析器边界：PL1 15–220、PL2 15–250、PL4 60–250、温度偏移 0–30。这些是文件格式边界，**不是本机硬件推荐或允许范围**。

编辑前复制文件。无效 JSON 保留为 models.json.bad 并使用内置数据。使用 JIYAOCHU_DATA_DIR 可隔离开发数据。
