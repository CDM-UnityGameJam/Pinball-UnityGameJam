using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // One enemy type. Its look (model, size of the visual) belongs to the prefab; colour comes from its element.
    // This asset holds the numbers the rules use.
    [CreateAssetMenu(fileName = "Enemy", menuName = "Pinball/Enemy", order = 11)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName;
        [AssetsOnly, Tooltip("Enemy prefab (an EnemyView variant).")]
        public GameObject prefab;

        [Title("Stats")]
        [MinValue(1), Tooltip("Health at difficulty 1×. New enemies get this × the spawn profile's health multiplier.")]
        public int health = 9;
        [MinValue(0.01), SuffixLabel("m"), Tooltip("Collision and spacing radius.")]
        public float radius = 0.75f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Minimum time between two ball hits that deal damage to this enemy.")]
        public float hitCooldown = 0.2f;
        [MinValue(0)] public int scoreValue = 50;
        [MinValue(0), SuffixLabel("m/s"), Tooltip("A ball hitting this enemy leaves at least this fast, pushed away like a pop bumper, so balls don't stall between packed enemies. 0 = plain bounce.")]
        public float hitBounceSpeed = 14f;

        [Title("Element")]
        [Tooltip("An enemy is immune to damage and statuses of its own element.")]
        public ElementRule elementRule = ElementRule.RandomFromGame;
        [ShowIf(nameof(elementRule), ElementRule.Fixed)] public ElementDefinition fixedElement;

        [Title("March")]
        [MinValue(0.1), SuffixLabel("s"), Tooltip("Time to travel from the spawn row to the danger line at difficulty 1×, when nothing blocks it.")]
        public float descendSeconds = 35f;
        [MinValue(0), SuffixLabel("m"), Tooltip("Extra gap kept to the enemy in front while queueing.")]
        public float queueGap = 0.1f;

        [Title("Spawn spacing")]
        [MinValue(0), SuffixLabel("m")] public float horizontalGap = 0.45f;
        [MinValue(0), SuffixLabel("m")] public float verticalGap = 0.35f;
        [MinValue(0), SuffixLabel("m"), Tooltip("Free space kept around balls when spawning.")]
        public float ballClearance = 0.45f;
        [MinValue(0), SuffixLabel("m"), Tooltip("Free space kept to walls and blockers when spawning.")]
        public float wallClearance = 0.3f;

        [Title("Unstick")]
        [InfoBox("A ball resting against this enemy slower than Speed gets pushed away, so it can't get stuck.")]
        public UnstickSettings unstick = new UnstickSettings();

        public enum ElementRule { RandomFromGame, Fixed }
    }

    [Serializable]
    public sealed class UnstickSettings
    {
        [MinValue(0), SuffixLabel("m/s")] public float speed = 5f;
        [MinValue(0), SuffixLabel("m/s²")] public float acceleration = 20f;
        [Tooltip("Bias toward the top of the board, so opposing enemies don't cancel out.")]
        public float forwardBias = 0.35f;
        public float sideBias = 0.15f;
    }
}
