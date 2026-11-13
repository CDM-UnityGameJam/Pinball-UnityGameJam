using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace Pinball.Presentation
{
    // Floating text: damage and kill numbers on enemies, each batch's result, multi-kills, extra balls,
    // contact charge and paid combos.
    // Popups rise and fade over `lifetime` of real time, so they finish even when the game stops.
    public sealed class PopupLayer : HudPresenter
    {
        [Required, AssetsOnly] public TMP_Text popupPrefab;
        [MinValue(0.01), SuffixLabel("s")] public float lifetime = 1f;
        [Tooltip("Canvas units risen over the lifetime.")] public float rise = 40f;
        [Tooltip("Canvas units risen immediately.")] public float initialRise = 14f;
        [Tooltip("World offset above the point, along the board's up.")] public float heightAbove = 0.5f;
        [Tooltip("World offset toward the top of the board, so text clears the enemy's number.")] public float forwardOffset = 0.75f;
        [Tooltip("Board-space point for batch results.")] public Vector3 castResultAt = new Vector3(0f, 0f, 2f);
        [Tooltip("Board-space point for multi-kills and paid combos.")] public Vector3 multiKillAt = new Vector3(0f, 0f, 1f);
        [Tooltip("Contact charge appears this far down the board from the contact.")] public float chargeBackOffset = 1.2f;
        public Color hitColor = Color.white;
        [Tooltip("Shown where an extra ball appears.")] public string extraBallText = "+BALL";

        sealed class Popup
        {
            public TMP_Text text;
            public Vector3 world;
            public float age;
            public AttackRecord source;
        }

        readonly List<Popup> active = new List<Popup>();
        readonly HashSet<Enemy> watched = new HashSet<Enemy>();
        ObjectPool<TMP_Text> pool;
        Popup castToast;

        public int ActiveCount => active.Count;
        public IEnumerable<string> Texts { get { foreach (var p in active) yield return p.text.text; } }

        protected override void OnBind()
        {
            pool = new ObjectPool<TMP_Text>(
                () => Instantiate(popupPrefab, transform),
                t => t.gameObject.SetActive(true),
                t => t.gameObject.SetActive(false),
                t => { if (t) Destroy(t.gameObject); });
            Context.Enemies.Added += Watch;
            Context.Score.Killed += (enemy, reward, source) => Show(enemy.transform.position, "+" + reward, Theme.green);
            Context.Score.MultiKill += OnMultiKill;
            Context.Casts.BatchRevealed += OnBatchRevealed;
            Context.Hand.ContactCharged += (point, amount) => Show(point - Context.Board.Forward * chargeBackOffset, "+" + amount.ToString("G6"), Theme.blue);
            Context.Score.ComboPaid += points => Show(Context.Board.ToWorld(multiKillAt), "COMBO +" + points, Theme.gold);
            Context.Multiball.BallEarned += ball => Show(ball.transform.position, extraBallText, Theme.gold);
        }

        void Watch(Enemy enemy)
        {
            if (!watched.Add(enemy)) return;
            enemy.Health.Damaged += (amount, source) => Show(enemy.transform.position, "-" + amount, hitColor);
        }

        void OnBatchRevealed(DiceBatch batch)
        {
            if (castToast != null) { Remove(castToast); castToast = null; }
            var color = batch.tier == CastTier.Base ? Theme.dim : batch.tier == CastTier.Jackpot ? Theme.gold : Theme.blue;
            castToast = Show(Context.Board.ToWorld(castResultAt), $"{DiceRules.Label(batch.tier)} · {batch.Coverage} targets", color);
        }

        void OnMultiKill(AttackRecord source)
        {
            string label = $"{source.label} ×{source.Kills} MULTIKILL";
            // Batches report kills as their skills land; the label keeps updating in place.
            foreach (var p in active)
                if (ReferenceEquals(p.source, source)) { p.text.text = label; p.age = 0f; return; }
            Show(Context.Board.ToWorld(multiKillAt), label, Theme.gold).source = source;
        }

        Popup Show(Vector3 world, string message, Color color)
        {
            var text = pool.Get();
            text.text = message;
            text.color = color;
            text.transform.SetAsLastSibling();
            var popup = new Popup { text = text, world = world };
            active.Add(popup);
            Place(popup);
            return popup;
        }

        void Remove(Popup popup)
        {
            if (active.Remove(popup)) pool.Release(popup.text);
        }

        void LateUpdate()
        {
            if (!Ready) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.age += Time.unscaledDeltaTime;
                if (p.age >= lifetime)
                {
                    if (p == castToast) castToast = null;
                    Remove(p);
                    continue;
                }
                Place(p);
            }
        }

        void Place(Popup p)
        {
            var board = Context.Board;
            var start = Hud.WorldToCanvas(p.world + board.Up * heightAbove + board.Forward * forwardOffset);
            p.text.rectTransform.anchoredPosition = start + new Vector2(0f, initialRise + rise * p.age / lifetime);
            var color = p.text.color;
            color.a = 1f - p.age / lifetime;
            p.text.color = color;
        }
    }
}
