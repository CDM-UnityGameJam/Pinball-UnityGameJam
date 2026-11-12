using UnityEngine;

namespace Pinball.Feedback
{
    // A flat ring growing from `startScale` of its radius to the full radius over `duration`.
    // Radius comes from the request, or from `radius` / `strongRadius` when the request has none.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ExpandingRingEffect : MonoBehaviour, IVfxEffect
    {
        [Min(0.01f)] public float duration = 0.2f;
        [Min(3)] public int segments = 40;
        [Tooltip("Height above the surface.")] public float lift = 0.27f;
        [Range(0f, 1f)] public float startScale = 0.25f;
        [Tooltip("Used when the request has no radius.")] public float radius = 0.9f;
        public float strongRadius = 1.6f;

        LineRenderer line;
        VfxRequest request;
        float fullRadius, age;

        public void Begin(in VfxRequest r)
        {
            line ??= GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            request = r;
            fullRadius = r.radius > 0f ? r.radius : r.strong ? strongRadius : radius;
            age = 0f;
            line.enabled = true;
            Draw();
        }

        public bool Tick(float deltaTime)
        {
            age += deltaTime;
            if (age >= duration)
            {
                line.enabled = false;
                return false;
            }
            Draw();
            return true;
        }

        void Draw()
        {
            float r = fullRadius * Mathf.Lerp(startScale, 1f, age / duration);
            var center = request.position + request.Up * lift;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, center + (request.Right * Mathf.Cos(a) + request.Forward * Mathf.Sin(a)) * r);
            }
        }
    }
}
