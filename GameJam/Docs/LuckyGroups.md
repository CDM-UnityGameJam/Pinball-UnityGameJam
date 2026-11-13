# Whole-number groups and lucky draws

The scene keeps its CardBalanceConfig reference. The default asset is Assets/Settings/DefaultCardBalance.asset. All game UI remains English; the existing Chinese workbook is edited in place.

## Playing

The opening hand is randomly filled for free before launch, without selecting a lucky number or testing lucky candidates. Card numbers are 1–3 and are always displayed in ascending number order, with stable order within each number. Lucky-number selection starts with the first recharge refill.

Drag any card to move its whole same-number group together. Release outside the original hand rectangle to cast; release inside to return the group without spending cards. A click without dragging does not cast.

Press 1, 2 or 3 (top row or numpad) once to select and highlight the whole group. A hint appears. After selection, double-tap that same key within 0.3 seconds to cast; a single tap cancels after that short window expires. The initial selection press does not count as the first tap of the casting double-tap. A different number switches groups. Holding a key does not repeat casts. Mouse drag resets pending keyboard taps; pause, focus loss and the F8 panel cannot cause a delayed cast.

Selection and dragging continue to supply enchantments. A refill refreshes the selection and sorted display; submission independently rejects partial, mixed-number, stale or duplicate-instance groups without spending cards, energy or random draws. The former mouse confirmation button is now an instruction label.

Casting is immediate. Normal groups use Normal skills; the current lucky number uses Lucky skills. Each element resolves once with same-element stacking. Shockwave always impacts once. All effects in one group share their kill record. Targets and skill parameters are captured at submission; later draws or edits cannot change ongoing waves or burning.

## Draws and luck

CardBalanceConfig > Draws configures hand size (1–5, applied to a new run), three independent number weights, three element weights, candidate probability, lucky held bonuses, and five empty-slot costs. If the hand still contains a card of the current lucky number, refills preserve that number without testing candidates. Otherwise, each new card is independently tested. A successful card is selected uniformly from successful card instances, including duplicate numbers. Its number becomes the sole lucky number for the whole hand. No successful candidate means no lucky number. Spending the last lucky group clears the number after capturing the submitted lucky skill; the next refill can choose a new number. Casting does not roll luck.

The probability of any lucky result for n new cards is 1 - (1 - p)^n. At p=20%, one draw gives 20%; five draws give 67.232%.

Held effects first use total element count, then lucky element count adds Arc damage, fire DPS or stun duration. Existing statuses finish naturally. Fire still accumulates fractional damage and only deducts whole points. Fire and Shockwave immunity rules are unchanged.

## Refill

An empty-slot count directly selects the total charge requirement, initially 100/150/200/250/300. Further casts retain accumulated energy and replace the requirement. Natural recovery and physical-contact energy keep their workbook values; burns/skills/kills grant none. On completion, refill all empty slots together, choose luck from those new cards, and clear all charge and overflow. A charging collision and all old enchantments finish before the new cards join the hand.

Pause, opening launch wait and game over freeze gameplay. Life loss preserves hand, luck and charge. A normal relaunch continues combat. Restart draws a fresh full hand and clears pending skills. Existing time/combo scoring and difficulty behavior remain unchanged.

## Excel and Inspector

- Card basics (卡牌基础): new yellow draw/lucky input rows, number/element weight table, empty-slot charge table. Obsolete costs, global active damage and reveal delay inputs are retired.
- Active skills (主动技能): six Normal/Lucky rows, independently editable scope, damage/DPS, waves, burning and stun. Normal migrated from the previous Hit row and Lucky from Full Field; all other workbook tuning is preserved.
- Calculation preview (计算预览): total/lucky held counts, casts, new draws and empty slots; displays lucky probability, actual held effects, charge and captured-plan damage.
- Import through LuckyPinball > Configuration > Import Excel Workbook, validate, then apply both existing assets. Older workbook schemas are rejected with a clear request to use the updated template.
- Inspector exposes Draws and six active rows. F8 > Cards can set a temporary lucky number (0 = none), add a numbered card, fill the entire hand through normal recharge. These do not save assets.

## Verification

Standalone checks: Tests/GroupRulesRunner.cs. Native integration: LuckyPinball > Run > Check Lucky Groups (Play Mode). Earlier dice-era checks are archived and do not describe this version.
