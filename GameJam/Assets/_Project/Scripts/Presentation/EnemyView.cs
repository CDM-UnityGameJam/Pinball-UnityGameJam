using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UltEvents;
using UnityEngine;

namespace Pinball.Presentation
{
    // How an enemy looks and what designers can hook onto it. Reads gameplay state; never changes it.
    // Colours are applied with a MaterialPropertyBlock, so enemies share their materials.
    public sealed class EnemyView : MonoBehaviour
    {
        [Required] public Enemy enemy;
        [Tooltip("Shows the enemy's element symbol.")]
        public TMP_Text numberLabel;
        [Tooltip("The outer ring (body).")] public Renderer ring;
        [Tooltip("The inner cap; shows hit flash, burn and stun.")] public Renderer cap;
        [Tooltip("Optional monster sprite; tinted for preview, hit, burn and stun.")] public SpriteRenderer body;

        [Title("Colours")]
        [Tooltip("Ring colour when the enemy has no element. Otherwise the element's colour.")]
        public Color ringColor = new Color32(201, 71, 90, 255);
        [Tooltip("Ring colour while the selected cards would target this enemy.")]
        public Color previewColor = new Color32(233, 185, 73, 255);
        [Range(0f, 1f), Tooltip("Cap colour = black blended this far toward the ring colour.")]
        public float capShade = 0.35f;
        public Color flashColor = Color.white;
        public Color burnColor = new Color(1f, 0.32f, 0.03f);
        public Color stunColor = new Color(0.3f, 0.7f, 1f);
        [MinValue(0), SuffixLabel("s")] public float flashSeconds = 0.12f;
        [Tooltip("Monster sprite tint while flashing from a hit (sprites can only be darkened, not whitened).")]
        public Color hitTint = new Color(1f, 0.35f, 0.35f);
        [Range(0, 1), Tooltip("How strongly state colours tint the monster sprite.")]
        public float bodyTintStrength = 0.55f;

        [Title("Hit squash")]
        [Tooltip("The monster sprite squashes wide and short on every damaging hit, then springs back. Only the sprite " +
                 "moves; the collider never changes. Strength grows with the hit's damage up to Squash Max.")]
        [Range(0, 0.5f)] public float squashMin = 0.04f;
        [Range(0, 0.5f)] public float squashMax = 0.1f;
        [MinValue(1), Tooltip("Damage that gives the full Squash Max.")] public float squashFullDamage = 30f;
        [MinValue(1)] public float squashStiffness = 320f;
        [MinValue(0)] public float squashDamping = 14f;

        [Title("Hooks")]
        [Tooltip("Every hit that deals damage. Passes the enemy's position.")]
        public UltEvent<Vector3> onHit;
        public UltEvent<Vector3> onKilled;
        public UltEvent<Vector3> onBreach;
        [Tooltip("Every new ball contact on this enemy (also inside its damage cooldown). Passes the contact point.")]
        public UltEvent<Vector3> onBallHit;
        public UltEvent onBurnStart, onBurnEnd, onStunned;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        float flashLeft;
        ElementDefinition shownElement;
        bool wasBurning, wasStunned;
        Vector3 bodyRest;
        float squash, squashVel;

        // Set by the HUD while the selected cards would target this enemy.
        public bool Previewed { get; set; }

        void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            flashLeft = 0f;
            shownElement = null;
            wasBurning = wasStunned = false;
            Previewed = false;
            if (body)
            {
                if (bodyRest == Vector3.zero) bodyRest = body.transform.localScale;
                body.transform.localScale = bodyRest;
            }
            squash = squashVel = 0f;
            enemy.Health.Damaged += OnDamaged;
            enemy.Health.Died += OnDied;
            enemy.Breached += OnBreached;
            enemy.Contact.BallContact += OnBallHit;
        }

        void OnDisable()
        {
            enemy.Health.Damaged -= OnDamaged;
            enemy.Health.Died -= OnDied;
            enemy.Breached -= OnBreached;
            enemy.Contact.BallContact -= OnBallHit;
        }

        void OnBallHit(Ball ball, Vector3 point) => onBallHit?.Invoke(point);

        void OnDamaged(int amount, AttackRecord source)
        {
            flashLeft = flashSeconds;
            // Kick the spring's velocity so it overshoots into a squash and settles back.
            float strength = Mathf.Lerp(squashMin, squashMax, Mathf.Clamp01(amount / squashFullDamage));
            // Peak squash is about `strength`; hits landing mid-squash never push it past that.
            float kick = strength * Mathf.Sqrt(squashStiffness) * 1.6f;
            squashVel = Mathf.Max(squashVel, kick);
            onHit?.Invoke(transform.position);
        }

        void OnDied(AttackRecord source) => onKilled?.Invoke(transform.position);
        void OnBreached() => onBreach?.Invoke(transform.position);

        void Update()
        {
            if (numberLabel && enemy.Element != shownElement)
            {
                shownElement = enemy.Element;
                numberLabel.text = shownElement ? shownElement.symbol : "";
            }
            var baseColor = enemy.Element ? enemy.Element.color : ringColor;

            bool burning = enemy.Status.IsBurning, stunned = enemy.Status.IsStunned;
            if (burning != wasBurning) { if (burning) onBurnStart?.Invoke(); else onBurnEnd?.Invoke(); wasBurning = burning; }
            if (stunned && !wasStunned) onStunned?.Invoke();
            wasStunned = stunned;

            if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
            Paint(ring, Previewed ? previewColor : baseColor);
            Paint(cap, flashLeft > 0f ? flashColor : burning ? burnColor : stunned ? stunColor : Color.Lerp(Color.black, baseColor, capShade));
            if (body)
            {
                Color? tint = flashLeft > 0f ? hitTint : burning ? burnColor : stunned ? stunColor : Previewed ? previewColor : (Color?)null;
                body.color = tint.HasValue ? Color.Lerp(Color.white, tint.Value, bodyTintStrength) : Color.white;
            }
            UpdateSquash();
        }

        // Damped spring on unscaled time (keeps settling while paused). Wide-and-short, volume roughly kept.
        void UpdateSquash()
        {
            if (!body) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            squashVel += (-squash * squashStiffness - squashVel * squashDamping) * dt;
            squash += squashVel * dt;
            if (Mathf.Abs(squash) < 1e-4f && Mathf.Abs(squashVel) < 1e-3f) squash = squashVel = 0f;
            float s = Mathf.Clamp(squash, -0.5f, 0.5f);
            body.transform.localScale = new Vector3(bodyRest.x * (1f + s), bodyRest.y * (1f - s), bodyRest.z);
        }

        void Paint(Renderer target, Color color)
        {
            if (!target || !target.enabled) return;
            target.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            target.SetPropertyBlock(block);
        }
    }
}
