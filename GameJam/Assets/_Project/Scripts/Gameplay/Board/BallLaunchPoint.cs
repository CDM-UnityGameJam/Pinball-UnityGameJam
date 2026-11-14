using UnityEngine;

namespace Pinball.Gameplay
{
    // Where the ball waits and launches from. Launch direction is this transform's forward;
    // GameConfig's spread randomises it either side.
    public sealed class BallLaunchPoint : MonoBehaviour
    {
        [Tooltip("Gizmo only: spread to preview. The real value is GameConfig.launchSpreadDegrees.")]
        [SerializeField] float previewSpreadDegrees = 8f;

        public Vector3 Position => transform.position;

        // `degrees` from forward, turning about the board's up (positive = to the right).
        public Vector3 DirectionAt(float degrees) => transform.rotation * Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;

        // sample01 = 0 is the full spread to one side, 0.5 straight, 1 the other side.
        public Vector3 Direction(float spreadDegrees, float sample01) =>
            transform.rotation * Quaternion.Euler(0f, Mathf.Lerp(-spreadDegrees, spreadDegrees, sample01), 0f) * Vector3.forward;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.375f);
            foreach (float t in new[] { 0f, 0.5f, 1f })
                Gizmos.DrawRay(transform.position, Direction(previewSpreadDegrees, t) * 2.5f);
        }
    }
}
