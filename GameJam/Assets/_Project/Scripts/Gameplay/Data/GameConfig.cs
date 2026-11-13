using System;
using System.Collections.Generic;
using Pinball.Core;
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
        [MinValue(0), Tooltip("Lives lost when an enemy crosses the danger line. Only breaches cost lives, and every life lost clears the field.")]
        public int breachLifeCost = 1;
        [MinValue(0), SuffixLabel("s"), Tooltip("Wait before the first launch (real time), and before a replacement ball after the last one drains (combat time; the fight keeps going).")]
        public float launchCountdown = 3f;

        [Title("Ball launch")]
        [MinValue(0), SuffixLabel("m/s")] public float launchSpeed = 32f;
        [MinValue(0), SuffixLabel("deg"), Tooltip("The launch direction is randomised by up to this angle either side.")]
        public float launchSpreadDegrees = 8f;

        [Title("Multiball")]
        [InfoBox("Extra balls enter at the board's ExtraBallEntry anchor. Drained balls never cost lives.")]
        public MultiballRules multiball = new MultiballRules();

        [Title("Combat")]
        [MinValue(0), Tooltip("Damage a ball hit deals to the enemy it touched, before any enchantment.")]
        public int ballHitDamage = 4;
        [MinValue(0), Tooltip("Ball hit damage while an empowered hit is stocked (uses one).")]
        public int empoweredHitDamage = 8;
        [MinValue(0), Tooltip("Empowered hits stocked per card played.")]
        public int empoweredHitsPerCard = 1;
        [MinValue(0), SuffixLabel("s"), Tooltip("Combat time between playing a batch and its dice being revealed.")]
        public float diceRevealDelay = 0.45f;
        public StatusRules status = new StatusRules();

        [Title("Scoring")]
        public ScoringRules scoring = new ScoringRules();

        [Title("Content")]
        [Required] public DeckConfig deck;
        [Required] public SpawnProfile spawnProfile;
        [Required, Tooltip("Every element in play, in display order. Enemies are one of these and immune to it.")]
        public List<ElementDefinition> elements = new List<ElementDefinition>();
    }

    // When extra balls are earned and how they enter.
    [Serializable]
    public sealed class MultiballRules
    {
        [MinValue(1), MaxValue(3), Tooltip("Most balls on the table at once, counting balls waiting to enter.")]
        public int maxBalls = 3;
        [Tooltip("One attack (a ball hit with its enchantments, or a whole played batch) killing two enemies earns an extra ball, once per attack.")]
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
        public float passiveStunImmunity = 1f;
    }

    // Score on top of each enemy's kill value.
    [Serializable]
    public sealed class ScoringRules
    {
        [MinValue(0.01), SuffixLabel("s"), Tooltip("Combat seconds per time reward.")]
        public float timeInterval = 5f;
        [MinValue(0)] public int timePoints = 5;
        [MinValue(0.01), SuffixLabel("s"), Tooltip("A combo ends after this long without a counted ball hit, and pays its highest tier once.")]
        public float comboTimeout = 3f;
        [ValidateInput(nameof(ValidTiers)), ListDrawerSettings(ShowFoldout = false)]
        public List<ComboTier> comboTiers = new List<ComboTier>();

        bool ValidTiers(List<ComboTier> value, ref string message)
        {
            message = ComboScore.Check(value);
            return message == null;
        }
    }
}
