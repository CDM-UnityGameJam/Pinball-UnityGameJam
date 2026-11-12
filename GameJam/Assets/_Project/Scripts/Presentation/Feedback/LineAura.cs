using UnityEngine;

namespace Pinball.Presentation
{
    // Line-based ball auras, ported from the old prototype's shapes:
    // - ElectricRing: a jagged ring spinning around the ball (Arc).
    // - Pulse: a ring breathing in and out around the ball (Shockwave).
    // - Flame: a wavering flame line toward the top of the board, plus an optional trail (Spark).
    // Shape numbers are fields; width and colour are on the LineRenderer (and trail).
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LineAura : MonoBehaviour, IBallAura
    {
        public enum Shape { ElectricRing, Pulse, Flame }

        public Shape shape = Shape.ElectricRing;
        [Min(3)] public int points = 20;
        [Tooltip("Height above the ball centre.")] public float lift = 0.3f;
        [Tooltip("Ring: outer and inner radius (Pulse uses the first). Flame: unused.")]
        public Vector2 radius = new Vector2(0.48f, 0.36f);
        [Tooltip("Pulse: how much the ring breathes.")] public float pulse = 0.04f;
        [Tooltip("Flame: spacing along the board, and sideways sway.")] public Vector2 flame = new Vector2(0.13f, 0.18f);
        [Tooltip("Optional trail shown with this aura (cleared when it hides).")] public TrailRenderer trail;

        LineRenderer line;

        LineRenderer Line => line ? line : line = GetComponent<LineRenderer>();

        public bool Visible => Line.enabled;

        public void SetVisible(bool visible)
        {
            if (Line.enabled == visible) return;
            Line.enabled = visible;
            if (!trail) return;
            if (!visible) trail.Clear();
            trail.enabled = visible;
            trail.emitting = visible;
        }

        public void UpdateAura(Vector3 center, Quaternion frame, float phase)
        {
            var l = Line;
            l.useWorldSpace = true;
            l.positionCount = points;
            Vector3 up = frame * Vector3.up, right = frame * Vector3.right, forward = frame * Vector3.forward;
            switch (shape)
            {
                case Shape.ElectricRing:
                    l.loop = false;
                    for (int i = 0; i < points; i++)
                    {
                        float a = i * Mathf.PI * 2f / (points - 1) + phase;
                        float r = i % 2 == 0 ? radius.x : radius.y;
                        l.SetPosition(i, center + up * lift + (right * Mathf.Cos(a) + forward * Mathf.Sin(a)) * r);
                    }
                    break;
                case Shape.Pulse:
                    l.loop = true;
                    float pr = radius.x + pulse * Mathf.Sin(phase);
                    for (int i = 0; i < points; i++)
                    {
                        float a = i * Mathf.PI * 2f / points;
                        l.SetPosition(i, center + up * lift + (right * Mathf.Cos(a) + forward * Mathf.Sin(a)) * pr);
                    }
                    break;
                case Shape.Flame:
                    l.loop = false;
                    for (int i = 0; i < points; i++)
                        l.SetPosition(i, center + up * lift + forward * (i * flame.x - 0.1f) + right * Mathf.Sin(i * 2 + phase) * (flame.y * (1 - i / 8f)));
                    break;
            }
        }
    }
}
