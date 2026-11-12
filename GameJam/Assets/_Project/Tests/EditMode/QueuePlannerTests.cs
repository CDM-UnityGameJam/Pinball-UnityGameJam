using System.Collections.Generic;
using NUnit.Framework;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // Queue scenarios ported from LuckPrototypeCheck (the EnemyMovement block). v0.3 values:
    // radius 0.75, gap 0.1, speed (9.5 - -5) / 40 = 0.3625 m/s.
    public class QueuePlannerTests
    {
        const float Radius = 0.75f, Gap = 0.1f, Speed = (9.5f - -5f) / 40f;
        const float Spacing = Radius * 2 + Gap; // 1.6

        readonly List<float> planned = new List<float>();

        static QueueEntry E(float x, float z, bool stunned = false) => new QueueEntry(x, z, Radius, Speed, stunned);

        [Test]
        public void StunnedFrontHoldsTheQueueButNotOtherLanes()
        {
            var entries = new List<QueueEntry> { E(0, 0, stunned: true), E(0, 1.6f), E(0, 3.2f), E(3, 1.6f) };
            QueuePlanner.Plan(entries, 0.1f, Gap, planned);
            Assert.That(planned[0], Is.EqualTo(0f), "stunned front stays put");
            Assert.That(planned[1], Is.EqualTo(1.6f).Within(0.0002f), "rear waits at the safe gap");
            Assert.That(planned[2], Is.EqualTo(3.2f).Within(0.0002f), "third row waits too");
            Assert.That(planned[3], Is.LessThan(1.6f), "an independent lateral path keeps moving");
        }

        [Test]
        public void QueueKeepsSpacingAndNeverMovesBackwards()
        {
            var entries = new List<QueueEntry> { E(0, 0, stunned: true), E(0, 1.6f), E(0, 3.2f) };
            for (int step = 0; step < 400; step++)
            {
                QueuePlanner.Plan(entries, 0.01f, Gap, planned);
                for (int i = 0; i < entries.Count; i++)
                {
                    Assert.That(planned[i], Is.LessThanOrEqualTo(entries[i].z + 0.0001f), "never moves backwards");
                    var e = entries[i]; e.z = planned[i]; entries[i] = e;
                }
                Assert.That(entries[1].z - entries[0].z, Is.EqualTo(Spacing).Within(0.0002f));
                Assert.That(entries[2].z - entries[1].z, Is.EqualTo(Spacing).Within(0.0002f));
            }
        }

        [Test]
        public void WhenTheFrontLeavesTheRearResumesNormalSpeedWithoutCatchUp()
        {
            var entries = new List<QueueEntry> { E(0, 1.6f) };
            QueuePlanner.Plan(entries, 0.01f, Gap, planned);
            Assert.That(1.6f - planned[0], Is.EqualTo(Speed * 0.01f).Within(1e-6f));
        }

        [Test]
        public void ExistingOverlapIsNotPushedBack()
        {
            var entries = new List<QueueEntry> { E(0, 0), E(0, 0.5f) };
            QueuePlanner.Plan(entries, 0.1f, Gap, planned);
            Assert.That(planned[1], Is.EqualTo(0.5f), "holds still instead of teleporting back to the gap");
        }

        [Test]
        public void DiagonalNeighboursUseCircleSpacing()
        {
            // dx = 1.0 < 1.6: safe Z = front + sqrt(1.6^2 - 1^2) = front + 1.249.
            var entries = new List<QueueEntry> { E(0, 0, stunned: true), E(1f, 1.3f) };
            QueuePlanner.Plan(entries, 1f, Gap, planned);
            Assert.That(planned[1], Is.EqualTo(Mathf.Sqrt(1.6f * 1.6f - 1f)).Within(1e-5f));
        }
    }
}
