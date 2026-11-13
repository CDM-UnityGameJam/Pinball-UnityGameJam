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
        public TMP_Text numberLabel;
        [Tooltip("The outer ring (body).")] public Renderer ring;
        [Tooltip("The inner cap; shows hit flash, burn and stun.")] public Renderer cap;
        [Tooltip("Optional monster sprite; tinted for preview, hit, burn and stun.")] public SpriteRenderer body;

        [Title("Colours")]
        public Color ringColor = new Color32(201, 71, 90, 255);
        [Tooltip("Ring colour while a dragged card would target this enemy's number.")]
        public Color previewColor = new Color32(233, 185, 73, 255);
        public Color capColor = new Color32(90, 38, 50, 255);
        public Color flashColor = Color.white;
        public Color burnColor = new Color(1f, 0.32f, 0.03f);
        public Color stunColor = new Color(0.3f, 0.7f, 1f);
        [MinValue(0), SuffixLabel("s")] public float flashSeconds = 0.12f;
        [Tooltip("Monster sprite tint while flashing from a hit (sprites can only be darkened, not whitened).")]
        public Color hitTint = new Color(1f, 0.35f, 0.35f);
        [Range(0, 1), Tooltip("How strongly state colours tint the monster sprite.")]
        public float bodyTintStrength = 0.55f;

        [Title("Hooks")]
        [Tooltip("Every hit that deals damage. Passes the enemy's position.")]
        public UltEvent<Vector3> onHit;
        public UltEvent<Vector3> onKilled;
        public UltEvent<Vector3> onBreach;
        [Tooltip("A valid ball hit on this enemy. Passes the contact point.")]
        public UltEvent<Vector3> onBallHit;
        public UltEvent onBurnStart, onBurnEnd, onStunned;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        float flashLeft;
        int shownNumber = int.MinValue;
        bool wasBurning, wasStunned;

        // Set by the HUD while a dragged card's number matches this enemy.
        public bool Previewed { get; set; }

        void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            flashLeft = 0f;
            shownNumber = int.MinValue;
            wasBurning = wasStunned = false;
            Previewed = false;
            enemy.Health.Damaged += OnDamaged;
            enemy.Health.Died += OnDied;
            enemy.Breached += OnBreached;
            enemy.Contact.BallHit += OnBallHit;
        }

        void OnDisable()
        {
            enemy.Health.Damaged -= OnDamaged;
            enemy.Health.Died -= OnDied;
            enemy.Breached -= OnBreached;
            enemy.Contact.BallHit -= OnBallHit;
        }

        void OnBallHit(Ball ball, Vector3 point) => onBallHit?.Invoke(point);

        void OnDamaged(int amount, AttackRecord source)
        {
            flashLeft = flashSeconds;
            onHit?.Invoke(transform.position);
        }

        void OnDied(AttackRecord source) => onKilled?.Invoke(transform.position);
        void OnBreached() => onBreach?.Invoke(transform.position);

        void Update()
        {
            if (numberLabel && enemy.Number != shownNumber)
            {
                shownNumber = enemy.Number;
                numberLabel.text = shownNumber.ToString();
            }

            bool burning = enemy.Status.IsBurning, stunned = enemy.Status.IsStunned;
            if (burning != wasBurning) { if (burning) onBurnStart?.Invoke(); else onBurnEnd?.Invoke(); wasBurning = burning; }
            if (stunned && !wasStunned) onStunned?.Invoke();
            wasStunned = stunned;

            if (flashLeft > 0f) flashLeft -= Time.unscaledDeltaTime;
            Paint(ring, Previewed ? previewColor : ringColor);
            Paint(cap, flashLeft > 0f ? flashColor : burning ? burnColor : stunned ? stunColor : capColor);
            if (body)
            {
                Color? tint = flashLeft > 0f ? hitTint : burning ? burnColor : stunned ? stunColor : Previewed ? previewColor : (Color?)null;
                body.color = tint.HasValue ? Color.Lerp(Color.white, tint.Value, bodyTintStrength) : Color.white;
            }
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
