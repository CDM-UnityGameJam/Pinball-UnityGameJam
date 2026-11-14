using Pinball.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Presentation
{
    // Kick feedback for a wall Kicker: the bar's Visual pops toward the board centre (the side the ball is on),
    // wobbles back on a spring with a small thickness bulge, and flashes bright. Only the Visual child moves;
    // the collider root never does. Put it on the kicker, next to Kicker.
    public sealed class KickerView : MonoBehaviour
    {
        [Required] public Kicker kicker;
        [Required, Tooltip("The bar's Visual child (MeshRenderer).")] public Transform visual;

        [Title("Pop")]
        [MinValue(0), SuffixLabel("m"), Tooltip("Roughly how far the bar pops inward.")]
        public float punch = 0.22f;
        [Range(0, 1), Tooltip("Extra thickness at the peak of the pop.")]
        public float bulge = 0.35f;
        [MinValue(1)] public float stiffness = 420f;
        [MinValue(0)] public float damping = 14f;

        [Title("Flash")]
        public Color flashColor = Color.white;
        [Range(0, 1)] public float flashStrength = 0.7f;
        [MinValue(0.01f), SuffixLabel("s")] public float flashSeconds = 0.15f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Vector3 restPosition, restScale, inwardLocal;
        float pop, popVel, flash;
        Renderer visualRenderer;
        MaterialPropertyBlock block;
        Color baseColor = Color.white;

        public float Pop => pop;

        void Awake()
        {
            if (!visual) return;
            restPosition = visual.localPosition;
            restScale = visual.localScale;
            visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer && visualRenderer.sharedMaterial && visualRenderer.sharedMaterial.HasProperty(BaseColor))
                baseColor = visualRenderer.sharedMaterial.GetColor(BaseColor);
            block = new MaterialPropertyBlock();
            // The Visual already sits on the inner side of the collider: that offset points toward the board centre.
            var offset = new Vector3(restPosition.x, 0f, 0f);
            inwardLocal = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.right;
        }

        void OnEnable() { if (kicker) kicker.Kicked += OnKicked; }
        void OnDisable()
        {
            if (kicker) kicker.Kicked -= OnKicked;
            pop = popVel = flash = 0f;
            Apply();
        }

        void OnKicked(Vector3 contact) => Kick();

        // Public so tests and tools can trigger it.
        public void Kick()
        {
            // Velocity kick: the spring overshoots to about `punch`, then wobbles back.
            popVel = Mathf.Max(popVel, punch * Mathf.Sqrt(stiffness) * 1.6f);
            flash = 1f;
        }

        void Update()
        {
            if (!visual || (pop == 0f && popVel == 0f && flash == 0f)) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            popVel += (-pop * stiffness - popVel * damping) * dt;
            pop += popVel * dt;
            flash = Mathf.Max(0f, flash - dt / flashSeconds);
            if (Mathf.Abs(pop) < 1e-4f && Mathf.Abs(popVel) < 1e-3f) pop = popVel = 0f;
            Apply();
        }

        void Apply()
        {
            if (!visual) return;
            visual.localPosition = restPosition + inwardLocal * pop;
            float grow = punch > 0f ? 1f + bulge * Mathf.Clamp01(Mathf.Abs(pop) / punch) : 1f;
            visual.localScale = new Vector3(restScale.x * grow, restScale.y, restScale.z);
            if (!visualRenderer) return;
            if (flash <= 0f) { visualRenderer.SetPropertyBlock(null); return; }
            visualRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, Color.Lerp(baseColor, flashColor, flash * flashStrength));
            visualRenderer.SetPropertyBlock(block);
        }
    }
}
