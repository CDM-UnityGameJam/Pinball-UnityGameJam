using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Presentation
{
    // Juicy card motion for one hand slot. The slot root stays put (it is the click/hover area); the
    // `visual` child moves:
    // - hover lifts and scales the card and tilts it in 3D toward the pointer (the point under the pointer
    //   sinks away, like pressing on it);
    // - a selected card pops (scale punch) and stays raised;
    // - otherwise cards sway and bob a little, each on its own phase.
    // Everything is a damped spring on unscaled time, so it keeps moving while the game is paused.
    // CardTilt reads Tilt / Glow / Sheen to warp the card's images in perspective and drive its shaders.
    public sealed class CardMotion : MonoBehaviour
    {
        public RectTransform visual;
        [Tooltip("Shadow left on the slot while the card rises (optional).")]
        public RectTransform shadow;

        [Header("Lift (canvas units)")]
        public float hoverLift = 22f;
        public float selectedLift = 34f;
        public float idleBob = 2.5f;

        [Header("Scale")]
        public float hoverScale = 1.08f;
        public float selectedScale = 1.05f;
        [Tooltip("Scale kick when the card is selected or unselected.")]
        public float pop = 0.18f;

        [Header("Tilt (degrees)")]
        public float maxTilt = 16f;
        public float idleSway = 3f;
        [Tooltip("In-plane roll that follows the sideways tilt.")]
        public float roll = 0.18f;

        [Header("Springs")]
        public float stiffness = 220f;
        public float damping = 20f;

        [Header("Shader drive")]
        [Range(0f, 1f)] public float idleGlow = 0.3f;
        [Range(0f, 1f)] public float hoverGlow = 0.75f;
        [Range(0f, 1f)] public float idleSheen = 0.15f;

        public bool Hovered { get; set; }
        public bool Selected { get; set; }
        public bool Blocked { get; set; }
        public bool HasCard { get; set; }

        // Normalised pointer position over the card, -1..1 on each axis.
        public Vector2 Pointer { get; set; }

        public Vector2 Tilt => tilt;
        public float Glow => glow;
        public float Sheen => sheen;

        readonly List<CardTilt> tilts = new List<CardTilt>();
        float lift, liftVel, scale = 1f, scaleVel, glow, glowVel, sheen, sheenVel;
        Vector2 tilt, tiltVel;
        float phase;

        void Awake()
        {
            phase = transform.GetSiblingIndex() * 1.7f;
            if (visual) visual.GetComponentsInChildren(true, tilts);
        }

        public void Pop() => scaleVel += pop * 40f;

        void Update()
        {
            if (!visual) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Time.unscaledTime;
            bool live = HasCard && !Blocked;

            float targetLift = !HasCard ? 0f : Selected ? selectedLift + (Hovered ? 6f : 0f) : Hovered ? hoverLift : 0f;
            float targetScale = !HasCard ? 1f : Hovered ? hoverScale : Selected ? selectedScale : 1f;
            var targetTilt = Hovered && HasCard
                ? new Vector2(Pointer.y * maxTilt, -Pointer.x * maxTilt)
                : new Vector2(Mathf.Sin(t * 1.3f + phase) * idleSway, Mathf.Cos(t * 1.1f + phase) * idleSway) * (HasCard ? 1f : 0f);
            float targetGlow = !live ? 0f : Selected ? 1f : Hovered ? hoverGlow : idleGlow;
            float targetSheen = !live ? 0f : Hovered || Selected ? 1f : idleSheen;

            Spring(ref lift, ref liftVel, targetLift, dt);
            Spring(ref scale, ref scaleVel, targetScale, dt);
            Spring(ref tilt.x, ref tiltVel.x, targetTilt.x, dt);
            Spring(ref tilt.y, ref tiltVel.y, targetTilt.y, dt);
            Spring(ref glow, ref glowVel, targetGlow, dt);
            Spring(ref sheen, ref sheenVel, targetSheen, dt);

            float bob = HasCard ? Mathf.Sin(t * 2f + phase) * idleBob : 0f;
            visual.anchoredPosition = new Vector2(0f, lift + bob);
            visual.localScale = Vector3.one * scale;
            visual.localRotation = Quaternion.Euler(0f, 0f, -tilt.y * roll);
            if (shadow)
            {
                shadow.anchoredPosition = new Vector2(tilt.y * 0.4f, -6f - lift * 0.15f);
                shadow.localScale = Vector3.one * (scale * (1f + lift * 0.004f));
            }
            foreach (var tiltEffect in tilts) if (tiltEffect) tiltEffect.Refresh();
        }

        void Spring(ref float value, ref float velocity, float target, float dt)
        {
            velocity += ((target - value) * stiffness - velocity * damping) * dt;
            value += velocity * dt;
        }
    }
}
