using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Root of Board.prefab. Holds the named anchors the rules read, so nothing uses typed-in coordinates.
    // The board's local space is "board space": X across, Y up off the playfield, Z toward the top.
    public sealed class BoardRoot : MonoBehaviour
    {
        [Required] public BallLaunchPoint launchPoint;
        [Required, Tooltip("Where extra balls (multiball) wait and drop in from. Its forward points down the table.")]
        public BallLaunchPoint extraBallEntry;
        [Required] public SpawnZone spawnZone;
        [Required] public DangerLine dangerLine;
        [Required] public DrainZone drain;
        [Required, Tooltip("Where dragged cards can be dropped to play them.")]
        public PlayfieldArea playfieldArea;
        [Required, Tooltip("The surface enemies stand on (the playfield's collider).")]
        public Collider playfieldSurface;
        public List<Flipper> flippers = new List<Flipper>();
        public List<ReturnLane> returnLanes = new List<ReturnLane>();

        // Board-space height of the playfield surface at (x, z); 0 (the floor) where there is none.
        public float SurfaceHeightAt(float x, float z)
        {
            var from = ToWorld(new Vector3(x, 5f, z));
            return playfieldSurface && playfieldSurface.Raycast(new Ray(from, -Up), out var hit, 10f)
                ? ToBoard(hit.point).y
                : 0f;
        }

        public Vector3 Up => transform.up;
        public Vector3 Forward => transform.forward;
        public Vector3 ToBoard(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 ToWorld(Vector3 board) => transform.TransformPoint(board);
    }
}
