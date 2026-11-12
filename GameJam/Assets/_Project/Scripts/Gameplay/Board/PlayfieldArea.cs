using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Where a dragged card can be dropped to play it: a rectangle on the board plane centred on this
    // transform (board X across, board Z up the board).
    public sealed class PlayfieldArea : MonoBehaviour
    {
        [SuffixLabel("m")] public Vector2 size = new Vector2(20.2f, 23f);

        Transform Board => transform.parent;

        // True if the ray hits the board plane inside the area.
        public bool Contains(Ray ray)
        {
            var plane = new Plane(Board.up, Board.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            var local = Board.InverseTransformPoint(ray.GetPoint(distance)) - Board.InverseTransformPoint(transform.position);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }

        void OnDrawGizmos()
        {
            if (!Board) return;
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.6f);
            Gizmos.matrix = Board.localToWorldMatrix;
            Gizmos.DrawWireCube(Board.InverseTransformPoint(transform.position), new Vector3(size.x, 0.01f, size.y));
        }
    }
}
