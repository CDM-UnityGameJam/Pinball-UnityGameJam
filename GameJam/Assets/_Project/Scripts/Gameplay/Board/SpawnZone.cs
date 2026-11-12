using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Where enemies appear. The spawn row is this transform's position. The usable width at a given
    // depth follows the board's top arc: straight sides up to the arc centre, then a half-ellipse.
    public sealed class SpawnZone : MonoBehaviour
    {
        [Required, Tooltip("Centre of the top arc (in the board).")]
        public Transform arcCenter;
        [MinValue(0.01), SuffixLabel("m"), Tooltip("Half-width of the playable area (and the arc's horizontal radius).")]
        public float halfWidth = 6.5f;
        [MinValue(0.01), SuffixLabel("m"), Tooltip("How far the arc reaches above its centre.")]
        public float arcHeight = 3.6f;

        Transform Board => transform.parent;

        public float RowZ => Board.InverseTransformPoint(transform.position).z;

        // Half the usable width at a board-space depth z, before subtracting enemy size or clearance.
        public float HalfWidthAt(float boardZ)
        {
            float dz = boardZ - Board.InverseTransformPoint(arcCenter.position).z;
            if (dz <= 0f) return halfWidth;
            float t = Mathf.Clamp01(dz / arcHeight);
            return halfWidth * Mathf.Sqrt(1f - t * t);
        }

        void OnDrawGizmos()
        {
            if (!arcCenter || !Board) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.matrix = Board.localToWorldMatrix;
            var c = Board.InverseTransformPoint(arcCenter.position);
            var previous = new Vector3(halfWidth, c.y, c.z);
            for (int i = 1; i <= 32; i++)
            {
                float a = Mathf.PI * i / 32f;
                var p = new Vector3(Mathf.Cos(a) * halfWidth, c.y, c.z + Mathf.Sin(a) * arcHeight);
                Gizmos.DrawLine(previous, p);
                previous = p;
            }
            float row = RowZ, w = HalfWidthAt(row);
            Gizmos.DrawLine(new Vector3(-w, c.y, row), new Vector3(w, c.y, row));
        }
    }
}
