using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Designer and developer tools for testing rules in play mode (ArchitecturePlan §4.11).
    // Put it next to the GameContext. Buttons only work while playing.
    public sealed class DebugCheats : MonoBehaviour
    {
        [Required] public GameContext context;

        [Title("Luck")]
        [MinValue(0)] public int luckyNumber = 6;
        [Range(-10, 100)] public int nextQuality = 40;

        [Title("Cards")]
        public ElementDefinition cardElement;
        [MinValue(0)] public int cardNumber = 6;

        [Title("Enemies")]
        public EnemyDefinition enemy;
        [MinValue(0)] public int enemyNumber = 2;
        [Tooltip("Board-space X and Z.")] public Vector2 enemyPosition = new Vector2(0f, 5f);

        bool Ready => Application.isPlaying && context && context.IsInitialized;

        [ButtonGroup("Luck"), EnableIf(nameof(Ready))]
        void SetLuckyNumber() => context.Luck.SetLuckyNumber(luckyNumber);

        [ButtonGroup("Luck"), EnableIf(nameof(Ready))]
        void ForceNextQ() => context.Luck.ForceNextQuality(nextQuality);

        [ButtonGroup("Luck"), EnableIf(nameof(Ready))]
        void ClearForcedQ() => context.Luck.ClearForcedQuality();

        [ShowInInspector, ReadOnly, ShowIf(nameof(Ready))]
        string ForcedQ => Ready && context.Luck.ForcedQuality.HasValue ? context.Luck.ForcedQuality.Value.ToString() : "None";

        [Button, EnableIf(nameof(Ready))]
        void AddCard()
        {
            var element = cardElement ? cardElement : context.config.elements[0];
            context.Hand.Grant(new CardSpec(element, cardNumber));
        }

        [Button, EnableIf(nameof(Ready))]
        void SpawnEnemy()
        {
            var definition = enemy ? enemy : context.config.spawnProfile.enemies[0].enemy;
            context.Spawner.Spawn(definition, enemyNumber, new Vector3(enemyPosition.x, 0f, enemyPosition.y));
        }
    }
}
