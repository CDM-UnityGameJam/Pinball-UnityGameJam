using UnityEngine;

namespace Pinball.Feedback
{
    // A short zigzag flame rising from the target, shown for `duration` seconds (ignite and burn ticks).
    [RequireComponent(typeof(LineRenderer))]
    public sealed class FlameFlickerEffect : MonoBehaviour, IVfxEffect
    {
        [Min(0.01f)] public float duration = 0.22f;
        [Min(2)] public int points = 5;
        [Tooltip("Height of the first point above the surface.")] public float baseHeight = 0.2f;
        [Tooltip("Height between points.")] public float step = 0.12f;
        [Tooltip("Sideways zigzag amplitude.")] public float zigzag = 0.12f;

        LineRenderer line;
        float left;

        public void Begin(in VfxRequest request)
        {
            line ??= GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
                line.SetPosition(i, request.position + request.Up * (baseHeight + i * step) + request.Right * (i % 2 == 0 ? -zigzag : zigzag));
            line.enabled = true;
            left = duration;
        }

        public bool Tick(float deltaTime)
        {
            left -= deltaTime;
            if (left > 0f) return true;
            line.enabled = false;
            return false;
        }
    }
}
