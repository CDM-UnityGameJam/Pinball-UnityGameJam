using System;
using System.Collections.Generic;
using Pinball.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // What spawns, how often, and how that ramps up over a run. Swap the asset to change difficulty.
    // Where enemies spawn is the board's SpawnZone, not a number here.
    [CreateAssetMenu(fileName = "SpawnProfile", menuName = "Pinball/Config/Spawn Profile", order = 3)]
    public sealed class SpawnProfile : ScriptableObject
    {
        [Title("Opening")]
        [MinValue(0), Tooltip("Enemies placed when the run starts, in shuffled columns.")]
        public int openingCount = 1;
        [MinValue(0), SuffixLabel("m")] public float openingColumnSpacing = 2f;

        [Title("Spawning")]
        [MinValue(0.01), SuffixLabel("s"), Tooltip("Combat time between spawn attempts at difficulty 1×.")]
        public float baseSpawnInterval = 5f;
        [Range(0f, 1f), Tooltip("Chance that a spawn attempt spawns anything.")]
        public float spawnChance = 0.96f;
        [MinValue(1), Tooltip("Random positions tried per spawn before giving up.")]
        public int placementAttempts = 30;

        [ValidateInput(nameof(HasEnemy), "Add at least one enemy with a weight above zero.")]
        public List<WeightedEnemy> enemies = new List<WeightedEnemy>();

        [Title("Difficulty over combat time")]
        [InfoBox("Spawn interval = base / spawn multiplier. March speed = (spawn row to danger line) / enemy descend seconds × move multiplier. " +
                 "New enemies get their health × health multiplier (living ones keep theirs). Values are interpolated between stages.")]
        [MinValue(0.000001), Tooltip("Global spawn-rate multiplier on top of the stages.")]
        public float spawnSpeedMultiplier = 1f;
        [MinValue(0.000001), Tooltip("Global march-speed multiplier on top of the stages.")]
        public float moveSpeedMultiplier = 1f;
        [ValidateInput(nameof(ValidStages)), ListDrawerSettings(ShowFoldout = false)]
        public List<DifficultyStage> stages = new List<DifficultyStage> { new DifficultyStage(0f, 1f, 1f, 1f) };

        public DifficultySample Sample(double combatTime)
        {
            var s = DifficultyCurve.Sample(DifficultyCurve.Check(stages) == null ? stages : null, combatTime);
            s.spawnMultiplier *= Mathf.Max(1e-6f, spawnSpeedMultiplier);
            s.moveMultiplier *= Mathf.Max(1e-6f, moveSpeedMultiplier);
            return s;
        }

        bool HasEnemy(List<WeightedEnemy> value)
        {
            if (value == null) return false;
            foreach (var entry in value) if (entry.enemy && entry.weight > 0f) return true;
            return false;
        }

        bool ValidStages(List<DifficultyStage> value, ref string message)
        {
            message = DifficultyCurve.Check(value);
            return message == null;
        }
    }

    [Serializable]
    public struct WeightedEnemy
    {
        [HorizontalGroup, HideLabel, Required] public EnemyDefinition enemy;
        [HorizontalGroup(90), MinValue(0), LabelWidth(45)] public float weight;
    }
}
