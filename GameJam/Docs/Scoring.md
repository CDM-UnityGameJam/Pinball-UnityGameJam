# Time and combo scoring

Normal enemy kills still award the GameManager's Normal Kill Points (50 by default).
CardBalanceConfig > Settings > Scoring owns the additional scoring rules:

- Time Interval: 10 seconds of gameplay per Time Points reward (1 by default).
- Combo Timeout: 3 gameplay seconds without an accepted physical damage hit.
- Combo Tiers: increasing hit thresholds and their rewards, initially 10 hits / 5 points and 50 hits / 10 points.

Each physical hit passing the existing damage cooldown adds one combo hit, including lethal hits. Enchantments, burning, active skills and cooldown-only contacts do not add hits. Reaching a threshold does not pay immediately. On timeout, only the highest reached tier pays once, then the combo clears.

Losing a life preserves the combo and its normal timeout. Game over settles the current combo once. Pause and the initial launch wait freeze all scoring clocks. Relaunch during normal gameplay continues them. Restart resets all progress.

The Chinese workbook's Scoring Configuration sheet (计分配置) edits the same settings through the existing Excel import menu. Add consecutive threshold/reward rows directly below the last tier; do not leave gaps. Inspector edits affect subsequent scoring updates without resetting accumulated points. The existing combo timeout uses the currently configured duration; the final reward uses the table at settlement.

The HUD displays current combo hits, remaining time and the pending highest-tier reward. MULTIKILL remains a separate attack-level visual and has no score multiplier.

Development verification: LuckyPinball > Run > Check Time and Combo Scoring (Play Mode).
