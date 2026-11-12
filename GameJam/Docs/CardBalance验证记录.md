# 卡牌配置验证记录

## 已交付

- 默认资产：`Assets/Settings/DefaultCardBalance.asset`。
- 场景：`Assets/Scenes/Pinball.unity` 的 GameManager.cardBalanceConfig 已指向该资产。
- 复用 LuckTuning 作为数据与计算逻辑，原场景内嵌 luck 配置已移除。
- Inspector 分组、独立元素档位、持牌五行表、公式和实际效果预览。
- 主动批次与状态参数快照、现有补牌进度保留。
- F8 Cards 页与受编辑器／开发构建限制的调试入口。
- 中文使用说明：`Docs/CardBalance使用说明.md`。

## 已通过

| 检查 | 结果 |
|---|---|
| 全部运行及编辑器脚本离线编译 | 通过；使用安装的 Unity 6000.6.8f1 引用程序集，无编译错误，仅原 SideReturnLane 的弃用 API 警告 |
| 迁移前后场景标量参数 | 24 项逐项一致 |
| 三个元素的四档配置迁移 | 12 行目标模式、数量、相关波数、持续时间与眩晕时间一致 |
| 场景配置 GUID、资产脚本 GUID | 一致；原 luck 序列化块已移除 |
| 默认技能计算 | 3 元素 × 4 档 × 1～5 张，共 60 组计划通过旧有效公式对照 |
| 持牌数量表 | 1～5 张的雷目标数、火附近目标数、震波半径通过 |
| 基础火独立目标数 | 改火目标数不改变雷目标数 |
| 已生成技能计划快照 | 改配置后，旧计划伤害／间隔／波数／燃烧参数保留，新计划读取新值 |
| 燃烧预览 | 实际每跳取整共用同一函数；低正数 DPS 取整为零可见 |
| 充能计算 | 预算与最低收益公式通过；当前进度、需求保留，下一张需求使用新基础值；零需求可安全结算 |
| 原骰子及选择规则 | 1～5 骰穷举、同号与实例校验通过 |
| 原 v0.6 充能规则回归 | 通过，包括 30,000 事件检查、2.5/2.5/1/0.25、滚动记录保留、自然恢复独立和补牌节奏 |

以上为离线编译、可独立运行的规则测试和迁移文件核对，不等同于 Unity 原生运行验收。

## 尚未完成

Unity MCP 返回 no_unity_session。一次隐藏批处理启动在运行集成测试前以退出码 1 结束，日志明确记录：

- `Code 10 while verifying Licensing Client signature`
- `LicensingClient has failed validation; ignoring`
- `Unexpected message Type: ApiErrorResponse, expected: HandshakeResponse`

日志：`tmp/card-balance-unity.log`。

以下集成检查代码已准备并编译，但本轮没有执行成功，因此未标为通过：

1. 修改雷伤害后，实际扣血与卡牌描述同步。
2. 已提交技能在修改配置后仍按旧伤害、波次、间隔、揭骰时间结算。
3. 已有燃烧／眩晕保留参数，新刷新使用新参数，连续燃烧首跳只充能一次。
4. 运行中修改恢复速度和补牌需求的实际行为。
5. F8 添加指定卡、一次性骰面、正常补牌调试入口和清空强化。
6. 与原 v0.6 元素免疫、多球共享强化、连杀归属等战斗检查一起运行。
7. Inspector、HUD 动态说明与 F8 Cards 页的实际布局、交互和试玩效果。
8. 切换配置后开新局、重开恢复随机骰子的实际场景检查。

编辑器恢复可用后，在 Pinball Play Mode 中运行 `LuckyPinball → Card Balance → Run Integration Checks (Play Mode)`；批处理入口为 `CardBalanceChecks.RunBatch`。
