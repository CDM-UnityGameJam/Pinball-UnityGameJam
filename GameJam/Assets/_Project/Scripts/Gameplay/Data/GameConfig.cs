using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Run-level settings, and the entry point to every other config for one way of playing the game.
    // Swap this asset (or the assets it points to) to make a preset or difficulty variant.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Pinball/Config/Game Config", order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Title("Run")]
        [MinValue(1)] public int lives = 3;
        [MinValue(0), Tooltip("Lives lost when the last ball drains.")]
        public int drainLifeCost = 1;
        [MinValue(0), Tooltip("Lives lost when an enemy crosses the danger line.")]
        public int breachLifeCost = 1;
        [MinValue(0), SuffixLabel("s"), Tooltip("Wait before each ball launches (run start and after a drain).")]
        public float launchCountdown = 3f;

        [Title("Ball launch")]
        [MinValue(0), SuffixLabel("m/s")] public float launchSpeed = 32f;
        [MinValue(0), SuffixLabel("deg"), Tooltip("The launch direction is randomised by up to this angle either side.")]
        public float launchSpreadDegrees = 8f;

        [Title("Multiball")]
        [InfoBox("Extra balls enter at the board's ExtraBallEntry anchor. A drain only costs a life when it was the last ball.")]
        public MultiballRules multiball = new MultiballRules();

        [Title("Combat")]
        [MinValue(0), Tooltip("Damage a valid ball hit deals to the enemy it touched, before any enchantment.")]
        public int ballHitDamage = 4;
        public StatusRules status = new StatusRules();

        [Title("Content")]
        [Required] public LuckConfig luck;
        [Required] public DeckConfig deck;
        [Required] public SpawnProfile spawnProfile;
        [Required, Tooltip("Every element in play, in display order.")]
        public List<ElementDefinition> elements = new List<ElementDefinition>();
    }

    // When extra balls are earned and how they enter.
    [Serializable]
    public sealed class MultiballRules
    {
        [MinValue(1), Tooltip("Most balls on the table at once, counting balls waiting to enter.")]
        public int maxBalls = 3;
        [Tooltip("Killing an enemy whose number equals the lucky number earns an extra ball.")]
        public bool ballOnLuckyKill = true;
        [Tooltip("One attack killing two or more enemies earns an extra ball (once per attack).")]
        public bool ballOnMultiKill = true;
        [MinValue(0), SuffixLabel("s"), Tooltip("Gameplay seconds an extra ball is held at the entry before it drops in.")]
        public float entryHold = 1f;
        [MinValue(0), SuffixLabel("m/s"), Tooltip("Speed an extra ball drops in at, along the entry's forward.")]
        public float entrySpeed = 4f;
        [MinValue(0), SuffixLabel("m"), Tooltip("Free space needed around the entry (beyond two ball radii) before a held ball drops.")]
        public float entryClearance = 0.1f;
    }

    // Shared burn/stun timing. Per-effect strength (DPS, durations) lives on the element's effect modules.
    [Serializable]
    public sealed class StatusRules
    {
        [MinValue(0.01), SuffixLabel("s"), Tooltip("Seconds between burn damage ticks.")]
        public float burnTickInterval = 0.5f;
        [MinValue(0), SuffixLabel("s"), Tooltip("After any stun ends, ball-hit (passive) stuns can't land for this long.")]
        public float passiveStunImmunity = 2f;
    }
}
