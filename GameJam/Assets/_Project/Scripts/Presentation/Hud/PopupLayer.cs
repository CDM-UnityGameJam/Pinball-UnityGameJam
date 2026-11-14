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
    // Each kind moves its own way (Style): hit numbers spring in sized by damage and scatter a little, kill points
    // bounce bigger, charge numbers just fade in, and banners (cast result, multi-kill, combo, +BALL) pop large and
    // settle; a multi-kill re-pops every time its count goes up.
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
        [Tooltip("Hit numbers by size: small, medium, big (big hits also get Big Hit Suffix).")]
        public Color hitColor = new Color(1f, 0.93f, 0.55f);
        public Color hitColorMedium = new Color(1f, 0.62f, 0.2f);
        public Color hitColorBig = new Color(1f, 0.28f, 0.42f);
        [MinValue(1)] public int mediumHitDamage = 12;
        [MinValue(1)] public int bigHitDamage = 30;
        public string bigHitSuffix = "!";
        [Tooltip("Big hits: seconds of white flash and shake as they appear.")] public float bigHitFlash = 0.12f;
        [Tooltip("Big hits: shake amplitude in canvas units.")] public float bigHitShake = 6f;

        [Title("Motion")]
        [MinValue(0.01), SuffixLabel("s"), Tooltip("Spring-in time: from nothing, past the peak, to the resting size.")]
        public float popTime = 0.22f;
        [Range(0, 1), Tooltip("Share of the lifetime before the fade starts.")] public float holdShare = 0.55f;
        [Tooltip("Hit numbers: resting size at 0 damage and at Hit Full Damage.")] public Vector2 hitSize = new Vector2(0.85f, 1.6f);
        [MinValue(1)] public float hitFullDamage = 40f;
        [Tooltip("Hit numbers: random sideways drift (canvas units) and tilt (degrees).")] public float hitDrift = 22f;
        public float hitTilt = 10f;
        [Min(1)] public float hitOvershoot = 1.45f;
        [Min(1)] public float killOvershoot = 1.7f;
        [Min(1)] public float bannerOvershoot = 1.6f;
        [Tooltip("Shown where an extra ball appears.")] public string extraBallText = "+BALL";

        enum Style { Hit, Kill, Charge, Banner }

        sealed class Popup
        {
            public TMP_Text text;
            public Vector3 world;
            public float age;
            public AttackRecord source;
            public Style style;
            public float size = 1f, overshoot = 1f, drift, tilt;
            public bool big;
            public Color color;
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
            Context.Score.Killed += (enemy, reward, source) => Show(enemy.transform.position, "+" + reward, Theme.green, Style.Kill);
            Context.Score.MultiKill += OnMultiKill;
            Context.Casts.BatchCast += OnBatchCast;
            Context.Hand.ContactCharged += (point, amount) => Show(point - Context.Board.Forward * chargeBackOffset, "+" + amount.ToString("G6"), Theme.blue, Style.Charge);
            Context.Score.ComboPaid += points => Show(Context.Board.ToWorld(multiKillAt), "COMBO +" + points, Theme.gold, Style.Banner);
            Context.Multiball.BallEarned += ball => Show(ball.transform.position, extraBallText, Theme.gold, Style.Banner);
        }

        void Watch(Enemy enemy)
        {
            if (!watched.Add(enemy)) return;
            enemy.Health.Damaged += (amount, source) =>
            {
                bool big = amount >= bigHitDamage;
                var color = big ? hitColorBig : amount >= mediumHitDamage ? hitColorMedium : hitColor;
                var p = Show(enemy.transform.position, "-" + amount + (big ? bigHitSuffix : ""), color, Style.Hit);
                p.size = Mathf.Lerp(hitSize.x, hitSize.y, Mathf.Clamp01(amount / hitFullDamage));
                p.big = big;
                if (big) p.overshoot *= 1.2f;
            };
        }

        void OnBatchCast(CastBatch batch)
        {
            if (castToast != null) { Remove(castToast); castToast = null; }
            var color = batch.Lucky ? Theme.gold : Theme.blue;
            castToast = Show(Context.Board.ToWorld(castResultAt), $"{GroupRules.Label(batch.tier)} #{batch.number} · {batch.Coverage} targets", color, Style.Banner);
        }

        void OnMultiKill(AttackRecord source)
        {
            string label = $"{source.label} ×{source.Kills} MULTIKILL";
            // Groups report kills as their skills land; the label keeps updating in place.
            foreach (var p in active)
                if (ReferenceEquals(p.source, source)) { p.text.text = label; p.age = 0f; return; } // age 0 re-pops it
            Show(Context.Board.ToWorld(multiKillAt), label, Theme.gold, Style.Banner).source = source;
        }

        Popup Show(Vector3 world, string message, Color color, Style style)
        {
            var text = pool.Get();
            text.text = message;
            text.color = color;
            text.transform.SetAsLastSibling();
            var popup = new Popup { text = text, world = world, style = style, color = color };
            switch (style)
            {
                case Style.Hit:
                    popup.overshoot = hitOvershoot;
                    popup.drift = Random.Range(-hitDrift, hitDrift);
                    popup.tilt = Random.Range(-hitTilt, hitTilt);
                    break;
                case Style.Kill: popup.overshoot = killOvershoot; popup.size = 1.15f; break;
                case Style.Banner: popup.overshoot = bannerOvershoot; break;
            }
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
            float t = p.age / lifetime;
            float riseScale = p.style == Style.Kill ? 1.5f : p.style == Style.Banner ? 0.5f : 1f;
            // Ease out: fast at first, then floating.
            float travel = 1f - (1f - t) * (1f - t);
            var rt = p.text.rectTransform;
            var shake = Vector2.zero;
            if (p.big && p.age < bigHitFlash)
                shake = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (bigHitShake * (1f - p.age / bigHitFlash));
            rt.anchoredPosition = start + shake + new Vector2(p.drift * travel, initialRise + rise * riseScale * travel);
            rt.localScale = Vector3.one * (p.size * PopScale(p));
            rt.localEulerAngles = new Vector3(0f, 0f, p.tilt * travel);
            var color = p.big && p.age < bigHitFlash ? Color.Lerp(Color.white, p.color, p.age / bigHitFlash) : p.color;
            color.a = p.style == Style.Charge ? Mathf.Clamp01(p.age / Mathf.Max(0.01f, popTime)) * Fade(t) : Fade(t);
            p.text.color = color;
        }

        // Spring-in: 0 to the overshoot peak, then back to 1 (charge numbers start at full size).
        float PopScale(Popup p)
        {
            if (p.style == Style.Charge || p.age >= popTime) return 1f;
            float t = p.age / popTime;
            const float peakAt = 0.45f;
            if (t < peakAt) { float u = t / peakAt; return Mathf.Lerp(0.2f, p.overshoot, 1f - (1f - u) * (1f - u)); }
            float v = (t - peakAt) / (1f - peakAt);
            return Mathf.Lerp(p.overshoot, 1f, v * v * (3f - 2f * v));
        }

        // Fully visible during the hold, then fade out.
        float Fade(float t) => t < holdShare ? 1f : 1f - (t - holdShare) / (1f - holdShare);
    }
}
