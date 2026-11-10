# Luck Number v0.3 号码限定附魔：出牌成本对照

以下为脚本化遭遇模拟，并非真实挡板／弹球试玩。每种策略使用5个固定随机种子，最长180秒；沿用场景生成间隔和下降时间，按固定横向列生成，拥挤时跳过生成，每1.2秒向最前排提交一次有效球命中。运行真实附魔、状态、技能波次、队列、概率和补牌代码；不模拟挡板操作和落球。因此死亡原因只能为越线，结论不能直接外推到真实玩家。

策略：见牌就打每0.5秒消耗全部可用牌；保留策略保持至少3张装备，仅敌群≥8或最前怪进入z≤2时考虑出牌，优先幸运同号。低装备严格按用户确认的0～1张计算。概率及补牌追加延迟保持设计值，未自动调参。

|策略|样本|出牌次数|平均手牌|0～1张持续秒数|运行秒数|击杀|死亡原因|
|---|---:|---:|---:|---:|---:|---:|---|
|Cast immediately|1|8|0.00|54.0|54.0|5|Enemy breach|
|Cast immediately|2|8|0.00|58.0|58.0|7|Enemy breach|
|Cast immediately|3|9|0.00|68.0|68.0|12|Enemy breach|
|Cast immediately|4|7|0.00|52.0|52.0|4|Enemy breach|
|Cast immediately|5|8|0.00|54.0|54.0|5|Enemy breach|
|Keep enchantments, cast selectively|1|6|3.42|0.0|80.0|18|Enemy breach|
|Keep enchantments, cast selectively|2|10|3.30|0.0|122.0|39|Enemy breach|
|Keep enchantments, cast selectively|3|10|3.30|0.0|122.0|39|Enemy breach|
|Keep enchantments, cast selectively|4|6|3.46|0.0|78.7|17|Enemy breach|
|Keep enchantments, cast selectively|5|6|3.59|0.0|76.0|16|Enemy breach|

|策略|平均出牌|平均手牌|平均低装备秒数|平均运行秒数|存活至180秒|
|---|---:|---:|---:|---:|---:|
|Cast immediately|8.0|0.00|57.2|57.2|0/5|
|Keep enchantments, cast selectively|7.6|3.41|0.0|95.7|0/5|

人工试玩可直接观察左侧本局记录；结束或重开时 Console 自动输出统计，或在 GameManager Inspector 点击记录按钮。应补充两种真实玩家策略的落球、越线和通关压力，再决定追加延迟改为3秒或提高。
