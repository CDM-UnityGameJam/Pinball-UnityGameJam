using UnityEngine;

namespace Pinball.Feedback
{
    // What a VFX play needs to know. Effects use the parts that apply to them: a bolt reads `from`, a ring
    // reads `radius`. `rotation` is the surface frame: its up is the board's up, its forward the board's
    // forward, so effects lie flat on a tilted board.
    public struct VfxRequest
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3? from;
        public float radius;
        public bool strong;

        public static VfxRequest At(Vector3 position, Quaternion rotation) =>
            new VfxRequest { position = position, rotation = rotation };

        public Vector3 Up => rotation * Vector3.up;
        public Vector3 Right => rotation * Vector3.right;
        public Vector3 Forward => rotation * Vector3.forward;
    }

    // A scripted part of a VFX prefab (line effects, etc.). Particle systems need no script.
    public interface IVfxEffect
    {
        void Begin(in VfxRequest request);

        // Advance by deltaTime; return false once finished.
        bool Tick(float deltaTime);
    }
}
