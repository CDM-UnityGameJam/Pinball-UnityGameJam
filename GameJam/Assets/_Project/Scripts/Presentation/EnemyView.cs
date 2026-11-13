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

        // Set by the HUD while the selected cards would target this enemy.
        public bool Previewed { get; set; }

        void OnEnable()
        {
            block ??= new MaterialPropertyBlock();
            flashLeft = 0f;
            shownElement = null;
            wasBurning = wasStunned = false;
            Previewed = false;
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
        }

        void Paint(Renderer target, Color color)
        {
            if (!target) return;
            target.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            target.SetPropertyBlock(block);
        }
    }
}
