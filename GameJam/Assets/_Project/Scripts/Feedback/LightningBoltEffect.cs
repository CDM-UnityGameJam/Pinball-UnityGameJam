using UnityEngine;

namespace Pinball.Feedback
{
    // A zigzag bolt from `request.from` to `request.position`, shown for `duration` seconds.
    // With no `from`, the bolt comes down from the sky: from `skyOffset` (or `strongSkyOffset` when strong),
    // in the request's surface frame. Width and colour are on the LineRenderer.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LightningBoltEffect : MonoBehaviour, IVfxEffect
    {
        [Min(0.01f)] public float duration = 0.12f;
        [Min(2)] public int points = 7;
        [Tooltip("Sideways zigzag amplitude.")] public float zigzag = 0.14f;
        [Tooltip("Height above the surface.")] public float lift = 0.22f;
        [Tooltip("Start point when no source is given (x right, y up, z toward the top of the board).")]
        public Vector3 skyOffset = new Vector3(0f, 0.5f, 1.3f);
        public Vector3 strongSkyOffset = new Vector3(0f, 2f, 3f);

        LineRenderer line;
        float left;

        public void Begin(in VfxRequest request)
        {
            line ??= GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            var to = request.position;
            var from = request.from ?? to + request.rotation * (request.strong ? strongSkyOffset : skyOffset);
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float side = i == 0 || i == points - 1 ? 0f : i % 2 == 0 ? zigzag : -zigzag;
                line.SetPosition(i, Vector3.Lerp(from, to, i / (float)(points - 1)) + request.Up * lift + request.Right * side);
            }
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
