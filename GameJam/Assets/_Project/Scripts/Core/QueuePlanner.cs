using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Core
{
    // One marching enemy, in board space (Z toward the top of the board; enemies march toward lower Z).
    public struct QueueEntry
    {
        public float x, z, radius, speed;
        public bool stunned;

        public QueueEntry(float x, float z, float radius, float speed, bool stunned)
        { this.x = x; this.z = z; this.radius = radius; this.speed = speed; this.stunned = stunned; }
    }

    // Enemy queue movement. Port of the old EnemyMovement.Plan:
    // - Entries are planned front first (closest to the danger line first), each against the already
    //   planned positions of every entry in front of it.
    // - An entry moves toward lower Z at its speed unless stunned.
    // - If it overlaps an entry in front laterally, it stops at a safe gap behind it (circle spacing:
    //   radius + radius + gap). It never moves backwards, even to fix an existing overlap.
    public static class QueuePlanner
    {
        // entries must be sorted front first. Writes the planned Z of each entry into plannedZ.
        public static void Plan(IReadOnlyList<QueueEntry> entries, float deltaTime, float gap, List<float> plannedZ)
        {
            plannedZ.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float next = e.stunned ? e.z : e.z - e.speed * deltaTime;
                for (int j = 0; j < i; j++)
                {
                    var front = entries[j];
                    float dx = Mathf.Abs(e.x - front.x);
                    float spacing = e.radius + front.radius + gap;
                    if (dx >= spacing) continue;
                    float safeZ = plannedZ[j] + Mathf.Sqrt(Mathf.Max(0f, spacing * spacing - dx * dx));
                    next = Mathf.Min(e.z, Mathf.Max(next, safeZ));
                }
                plannedZ.Add(next);
            }
        }
    }
}
