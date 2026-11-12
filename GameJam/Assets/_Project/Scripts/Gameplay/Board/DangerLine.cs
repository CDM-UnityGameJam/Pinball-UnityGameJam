using UnityEngine;

namespace Pinball.Gameplay
{
    // Enemies that reach this line breach. The visible red line is a child mesh the artist can replace.
    public sealed class DangerLine : MonoBehaviour
    {
        Transform Board => transform.parent;

        public float BoardZ => Board.InverseTransformPoint(transform.position).z;

        void OnDrawGizmos()
        {
            if (!Board) return;
            Gizmos.color = Color.red;
            Gizmos.matrix = Board.localToWorldMatrix;
            float z = BoardZ;
            Gizmos.DrawLine(new Vector3(-8f, 0.4f, z), new Vector3(8f, 0.4f, z));
        }
    }
}
