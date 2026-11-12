# Luck Number v0.6 implementation validation

Scene: Assets/Scenes/Pinball.unity. All player UI remains English.

Implemented:
- New ball/body contact charges independently of the shared 0.2-second enemy damage cooldown. Persistent contact is deduplicated until exit; the old 1-second charge cooldown is removed.
- Continuous burning grants charge on its first actual damage tick only, including lethal ticks. Full-hand first ticks consume the opportunity. Reignition after both sources expire starts a new episode.
- Natural recovery is separate from the rolling combat journal. Combat gains follow min(2.5, max(0.25, 6 - previous-second actual combat gains)). Casting and refilling preserve that journal.
- Zero matching dice resolve Base skills. Duplicate-element bonuses apply in all tiers; snapshots, immunity, reveal timing and unified kill attribution remain.
- Every accepted card grants one shared empowered physical hit immediately. Cooldown-only contacts do not consume it. Stock survives ball replacement and resets with the scene on restart.
- UI shows Upgrade chance, Base results, POWER stock and separate skill/matching-dice statistics.

Completed verification:
- Standalone rule suite passed exhaustive 1–5 dice combinations; two dice have 25 Base, 10 Full Field and 1 Jackpot outcomes.
- Recharge suite passed the 2.5/2.5/1/0.25 sequence, 30,000 event-formula checks, natural/combat separation, journal persistence, overflow and reference refill cadence.
- Runtime and editor scripts compiled against installed Unity 6000.6.8f1 assemblies with no errors. Existing obsolete API/development-build warnings remain.

Pending editor verification:
- Seven v0.6 acceptance groups are included in Assets/Scripts/Editor/V06Checks.cs and invoked by LuckyPinball > Luck Number > Run Acceptance Checks (Play Mode).
- These Play Mode checks have NOT run successfully yet. Unity MCP refresh timed out. A hidden batch launch also exited with code 1 during licensing/WMI initialization, including a retry outside the sandbox. See tmp/v06-unity.log.
- Visual layout, real physics enter/exit callbacks, and playable feel still require editor testing. No balance simulations or human-playtest results are claimed.
