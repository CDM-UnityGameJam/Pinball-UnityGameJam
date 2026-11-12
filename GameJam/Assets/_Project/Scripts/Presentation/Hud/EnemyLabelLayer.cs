using System.Collections.Generic;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace Pinball.Presentation
{
    // Number, health bar and status under every living enemy, following it on screen. Labels are pooled.
    public sealed class EnemyLabelLayer : HudPresenter
    {
        [Required, AssetsOnly] public EnemyLabelView labelPrefab;
        [Required] public HandView hand;
        [Tooltip("Canvas offset of the health bar from the enemy's near edge.")]
        public Vector2 belowOffset = new Vector2(0f, -6f);

        readonly Dictionary<Enemy, EnemyLabelView> labels = new Dictionary<Enemy, EnemyLabelView>();
        readonly List<Enemy> alive = new List<Enemy>();
        readonly List<Enemy> gone = new List<Enemy>();
        ObjectPool<EnemyLabelView> pool;

        public int Count => labels.Count;
        public EnemyLabelView LabelOf(Enemy enemy) => labels.TryGetValue(enemy, out var label) ? label : null;

        protected override void OnBind()
        {
            pool = new ObjectPool<EnemyLabelView>(
                () => Instantiate(labelPrefab, transform),
                l => l.gameObject.SetActive(true),
                l => l.gameObject.SetActive(false),
                l => { if (l) Destroy(l.gameObject); });
        }

        void LateUpdate()
        {
            if (!Ready) return;
            Context.Enemies.FrontFirst(alive);
            var boardForward = Context.Board.Forward;
            foreach (var enemy in alive)
            {
                if (!labels.TryGetValue(enemy, out var label)) labels[enemy] = label = pool.Get();
                var up = enemy.transform.up * enemy.centerHeight;
                var forward = boardForward * enemy.Radius;
                var position = enemy.transform.position;
                label.Show(
                    numberAt: Hud.WorldToCanvas(position + up),
                    barAt: Hud.WorldToCanvas(position - up - forward) + belowOffset,
                    statusAt: Hud.WorldToCanvas(position + up + forward),
                    enemy, hand.IsPreviewed(enemy), Theme);
            }
            gone.Clear();
            foreach (var pair in labels) if (!alive.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var enemy in gone)
            {
                pool.Release(labels[enemy]);
                labels.Remove(enemy);
            }
        }
    }
}
