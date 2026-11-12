using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // What spawns, how often, and how that changes over a run. Swap the asset to change difficulty.
    // Where enemies spawn is the board's SpawnZone, not a number here.
    [CreateAssetMenu(fileName = "SpawnProfile", menuName = "Pinball/Config/Spawn Profile", order = 3)]
    public sealed class SpawnProfile : ScriptableObject
    {
        [Title("Opening")]
        [MinValue(0), Tooltip("Enemies placed when the run starts, in shuffled columns.")]
        public int openingCount = 3;
        [MinValue(0), SuffixLabel("m")] public float openingColumnSpacing = 2f;

        [Title("Spawning")]
        [MinValue(0.1), SuffixLabel("s"), Tooltip("Time between spawn attempts.")]
        public float spawnInterval = 2f;
        [Range(0f, 1f), Tooltip("Chance that a spawn attempt spawns anything.")]
        public float spawnChance = 0.96f;
        [MinValue(1), Tooltip("Random positions tried per spawn before giving up.")]
        public int placementAttempts = 30;

        [ValidateInput(nameof(HasEnemy), "Add at least one enemy with a weight above zero.")]
        public List<WeightedEnemy> enemies = new List<WeightedEnemy>();

        [Title("Over time")]
        [InfoBox("Multipliers by minutes of active play. Flat at 1 = no change (v0.3 behaviour).")]
        [Tooltip("Multiplies the spawn interval.")]
        public AnimationCurve spawnIntervalOverTime = AnimationCurve.Constant(0f, 10f, 1f);
        [Tooltip("Multiplies enemy march speed.")]
        public AnimationCurve marchSpeedOverTime = AnimationCurve.Constant(0f, 10f, 1f);

        bool HasEnemy(List<WeightedEnemy> value)
        {
            if (value == null) return false;
            foreach (var entry in value) if (entry.enemy && entry.weight > 0f) return true;
            return false;
        }
    }

    [Serializable]
    public struct WeightedEnemy
    {
        [HorizontalGroup, HideLabel, Required] public EnemyDefinition enemy;
        [HorizontalGroup(90), MinValue(0), LabelWidth(45)] public float weight;
    }
}
