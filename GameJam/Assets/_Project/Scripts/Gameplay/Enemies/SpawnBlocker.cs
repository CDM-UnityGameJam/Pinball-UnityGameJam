using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Keeps enemies from spawning where they would march into something, e.g. a return-lane outlet.
    // Enemies march straight down the board, so by default the whole path from the spawn point to the
    // danger line is tested, not just the spawn point.
    public sealed class SpawnBlocker : MonoBehaviour
    {
        [MinValue(0), SuffixLabel("m"), Tooltip("Blocked radius around this point (added to the enemy's own radius and clearance).")]
        public float radius = 0.7f;
        [Tooltip("Test the enemy's whole straight path down to the danger line.")]
        public bool blocksDescentPath = true;

        Transform Board => GetComponentInParent<BoardRoot>().transform;

        public Vector3 BoardPosition => Board.InverseTransformPoint(transform.position);

        public bool Blocks(Vector3 spawnBoardPosition, float enemyRadiusWithClearance, float dangerZ)
        {
            var center = BoardPosition;
            float z = blocksDescentPath
                ? Mathf.Clamp(center.z, Mathf.Min(spawnBoardPosition.z, dangerZ), Mathf.Max(spawnBoardPosition.z, dangerZ))
                : spawnBoardPosition.z;
            float reach = enemyRadiusWithClearance + radius;
            return new Vector2(spawnBoardPosition.x - center.x, z - center.z).sqrMagnitude < reach * reach;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
