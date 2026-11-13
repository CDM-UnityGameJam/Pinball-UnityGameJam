using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // Moves every enemy toward the danger line each physics step, as a queue (Pinball.Core.QueuePlanner).
    // An enemy whose planned position reaches the danger line breaches.
    public sealed class EnemyMarch
    {
        readonly GameContext context;
        readonly List<Enemy> order = new List<Enemy>();
        readonly List<QueueEntry> entries = new List<QueueEntry>();
        readonly List<float> planned = new List<float>();

        public EnemyMarch(GameContext context) => this.context = context;

        // Speed for an enemy definition: from the spawn row to the danger line in descendSeconds,
        // times the current difficulty's move multiplier.
        public float SpeedOf(EnemyDefinition definition)
        {
            var board = context.Board;
            float distance = UnityEngine.Mathf.Abs(board.spawnZone.RowZ - board.dangerLine.BoardZ);
            return (float)(distance / UnityEngine.Mathf.Max(0.01f, definition.descendSeconds)
                   * context.config.spawnProfile.Sample(context.Clock.Now).moveMultiplier);
        }

        public void Step(float deltaTime)
        {
            if (!context.Run.IsPlaying) return;
            context.Enemies.FrontFirst(order);
            entries.Clear();
            float gap = 0f;
            foreach (var e in order)
            {
                var p = e.BoardPosition;
                entries.Add(new QueueEntry(p.x, p.z, e.Radius, SpeedOf(e.Definition), e.Status.IsStunned));
                gap = UnityEngine.Mathf.Max(gap, e.Definition.queueGap);
            }
            QueuePlanner.Plan(entries, deltaTime, gap, planned);

            float danger = context.Board.dangerLine.BoardZ;
            for (int i = 0; i < order.Count; i++)
            {
                if (!context.Run.IsPlaying) break;
                var e = order[i];
                if (planned[i] <= danger) e.Breach();
                else e.MoveTo(new UnityEngine.Vector3(entries[i].x, 0f, planned[i]));
            }
        }
    }
}
