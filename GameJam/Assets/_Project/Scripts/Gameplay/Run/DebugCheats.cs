using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Designer and developer tools for testing rules in play mode (ArchitecturePlan §4.11).
    // Put it next to the GameContext. Buttons only work while playing.
    public sealed class DebugCheats : MonoBehaviour
    {
        [Required] public GameContext context;

        [Title("Dice")]
        [Tooltip("Faces for the next batch, one per card, e.g. 4 6 for two cards. Used once.")]
        public List<int> nextDice = new List<int>();

        [Title("Cards")]
        public ElementDefinition cardElement;
        [MinValue(1)] public int cardNumber = 6;

        [Title("Enemies")]
        public EnemyDefinition enemy;
        [Tooltip("Element of the spawned enemy; empty = random.")]
        public ElementDefinition enemyElement;
        [Tooltip("Board-space X and Z.")] public Vector2 enemyPosition = new Vector2(0f, 5f);

        bool Ready => Application.isPlaying && context && context.IsInitialized;

        [Button, EnableIf(nameof(Ready)), Tooltip("The next batch with exactly this many cards rolls these faces.")]
        void ForceNextDice() => context.Hand.ForceNextDice(nextDice.ToArray());

        [ShowInInspector, ReadOnly, ShowIf(nameof(Ready))]
        string ForcedDice => Ready && context.Hand.ForcedDice != null ? string.Join(", ", context.Hand.ForcedDice) : "Random";

        [Button, EnableIf(nameof(Ready))]
        void AddCard()
        {
            var element = cardElement ? cardElement : context.config.elements[0];
            context.Hand.Grant(new CardSpec(element, cardNumber));
        }

        [Button, EnableIf(nameof(Ready)), Tooltip("Fill the charge so the next card arrives now.")]
        void FillCharge() => context.Hand.FillCharge();

        [Button, EnableIf(nameof(Ready))]
        void ClearEmpoweredHits() => context.Hand.ClearEmpoweredHits();

        [Button, EnableIf(nameof(Ready)), Tooltip("Queue an extra ball (respects the multiball cap).")]
        void AddBall() => context.Multiball.AddBall();

        [Button, EnableIf(nameof(Ready))]
        void SpawnEnemy()
        {
            var definition = enemy ? enemy : context.config.spawnProfile.enemies[0].enemy;
            var elements = context.config.elements;
            var element = enemyElement ? enemyElement : elements[context.Random.Range(0, elements.Count)];
            context.Spawner.Spawn(definition, element, new Vector3(enemyPosition.x, 0f, enemyPosition.y));
        }
    }
}
