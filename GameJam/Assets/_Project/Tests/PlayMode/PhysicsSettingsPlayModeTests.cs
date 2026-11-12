using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    public class PhysicsSettingsPlayModeTests
    {
        // A free-falling body must follow the Project Settings gravity at the 0.01 s step.
        [UnityTest]
        public IEnumerator BodyFallsWithProjectGravity()
        {
            var body = new GameObject("FallTest").AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            try
            {
                yield return new WaitForFixedUpdate();
                float speedBefore = -body.linearVelocity.y;
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
                float gained = -body.linearVelocity.y - speedBefore;
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(1e-6f));
                Assert.That(gained, Is.EqualTo(180f * 0.01f * 10).Within(0.5f));
            }
            finally { Object.Destroy(body.gameObject); }
        }
    }
}
