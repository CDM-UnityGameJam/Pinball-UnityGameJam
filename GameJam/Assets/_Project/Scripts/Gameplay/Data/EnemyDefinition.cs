using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // One enemy type. Its look (model, colours, size of the visual) belongs to the prefab;
    // this asset holds the numbers the rules use.
    [CreateAssetMenu(fileName = "Enemy", menuName = "Pinball/Enemy", order = 11)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName;
        [AssetsOnly, Tooltip("Enemy prefab (an EnemyView variant). Added in Phase 4.")]
        public GameObject prefab;

        [Title("Stats")]
        [MinValue(1)] public int health = 40;
        [MinValue(0.01), SuffixLabel("m"), Tooltip("Collision and spacing radius.")]
        public float radius = 0.75f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Minimum time between two ball hits counting on this enemy.")]
        public float hitCooldown = 0.2f;
        [MinValue(0)] public int scoreValue = 50;

        [Title("Number")]
        public NumberRule numberRule = NumberRule.RandomInLuckRange;
        [ShowIf(nameof(numberRule), NumberRule.Fixed)] public int fixedNumber = 1;

        [Title("March")]
        [MinValue(0.1), SuffixLabel("s"), Tooltip("Time to travel from the spawn row to the danger line when nothing blocks it.")]
        public float descendSeconds = 40f;
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

        public enum NumberRule { RandomInLuckRange, Fixed }
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
