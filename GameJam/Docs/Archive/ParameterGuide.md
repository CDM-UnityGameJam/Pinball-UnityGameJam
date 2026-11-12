> **Archived (outdated).** Describes an older version of the game, not Luck Number v0.3. The sources of truth are the live code and `Docs/LuckNumberPrototype.md`; see `Docs/ArchitecturePlan.md`.

# 参数修改入口

在 `Assets/Scenes/Pinball.unity` 中选择 **GameManager**，在 Inspector 修改参数并保存场景。
数值相同的不同玩法参数现在分别存储；修改普通怪生成间隔，不会改变特殊怪或弹跳怪的生成间隔。

Inspector 默认只显示玩法调参项。HUD 排版与颜色、模型细节、生成搜索次数、边界计算和组件引用等内部配置已隐藏；对某类怪物不生效的字段也按类型隐藏。隐藏只影响编辑器显示，不会删除或重置已保存的数值。下表同时保留完整参数说明，便于查阅内部配置；需要查看时可以使用 Inspector 的 Debug 模式。

| Inspector 分组 / 字段 | 用途 |
| --- | --- |
| Enemy Timing / Spawn Interval | 普通怪生成尝试间隔，当前场景保留为 1 秒 |
| Special Spawn Interval | 特殊怪概率判定间隔，独立计时 |
| Bumper Spawn Interval | 弹跳怪概率判定间隔，独立计时 |
| Enemy Descend Time / Bumper Descend Time | 普通怪、弹跳怪分别下降到底线的时间 |
| Match Kill Multiplier / Special Match Kill Multiplier | 普通怪、特殊怪分别匹配击杀时的奖励倍率 |
| Spawning | 各类生成次数、概率、特殊怪开局延迟、概率成长、目标数字及权重、生成区域和危险线 |
| Spawning / Special Targets | 每项分别设置 Target、Weight、Score Multiplier；可增删项，权重为 0 时禁用该项 |
| Normal Enemy / Special Enemy / Bumper | 三类怪分别设置血量、半径、厚度、颜色、伤害、能量、命中冷却、揭示时长、存活时长、防卡推力、球及边界间距 |
| Horizontal Gap / Vertical Gap | 横向、纵向的额外间距分别计算，不再互相相加；默认 0.45 / 0.35 |
| Hit Damage / Pulse Damage | 该类怪物受到的普通碰撞伤害、该类怪物匹配时发出的全场脉冲伤害 |
| Run / Cards / Scoring | 手牌数量、每张卡能量、发球等待、普通击杀分数、生存分数和间隔、功能卡持续时间、每张卡增加球数、排队发球间隔、两种扣命数量、物理步长 |
| Cards | 卡牌计算基数、初始当前数字、操作数范围、两种功能卡同时命中时的选择概率 |
| Functional Card Chances | 两种功能卡各自的出现概率 |
| HUD | 颜色、手牌尺寸、字体、位置、边距、弹字动画；分数、生命、计时显示及左右说明栏有独立样式 |

普通怪和弹跳怪现在分别抽取概率，默认每次 96% / 4%；两者可以同时出现。
特殊怪没有整列路径预留，仅检查当前位置是否有空间。
特殊怪的 `Special Start Delay` 默认为 0；需要开局保护时可以设置为例如 60 秒。

**编辑时机：** 生成间隔与概率等会在运行时读取。手牌数量、初始血量、模型尺寸、Canvas 布局等在初始化时应用，请停止 Play 后修改，再启动 Play 验证。Play 中的修改不会自动保存。

**已有场景物体：** 球的尺寸、质量、碰撞材质，挡板的角度与速度，球桌/墙/弧形边界的 Transform 与 Collider，以及摄像机参数，直接在各自物体或 Physics Material 的 Inspector 修改。它们没有额外的运行时代码常量覆盖。场景生成工具中的数字是新建场景的初始模板；重新 Build Scene 会重新生成场景。

**保留的代码数值：** 数组下标、循环起点、布尔状态、单位换算（百分比、秒/分钟）、半径到直径的几何换算、数学边界和测试样例属于程序结构，不作为游戏平衡参数开放。相关显示读取同一条游戏规则，避免文字与实际行为不一致。

**验证菜单：** `LuckyPinball/Check Configurable Rules` 验证非默认配置；`Check Independent Tuning (Play Mode)` 验证三类怪物属性、奖励倍率和生成计时互不干扰。后者会修改本次运行的数据，应在一次可丢弃的 Play 会话中执行。
