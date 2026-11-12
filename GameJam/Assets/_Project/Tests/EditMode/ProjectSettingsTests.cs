using NUnit.Framework;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // Guards the physics values ported from the old game (ArchitecturePlan §4.4, CLAUDE.md).
    // They live in Project Settings; no code may overwrite them at runtime.
    public class ProjectSettingsTests
    {
        static readonly string[] GameplayLayers = { "Ball", "Board", "Enemy", "Flipper", "Trigger" };

        [Test]
        public void FixedTimestepIsTenMilliseconds() =>
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(1e-6f));

        [Test]
        public void GravityMatchesOldScene() =>
            Assert.That(Physics.gravity, Is.EqualTo(new Vector3(0f, -180f, 0f)));

        [Test]
        public void GameplayLayersExist()
        {
            foreach (var layer in GameplayLayers)
                Assert.That(LayerMask.NameToLayer(layer), Is.Not.EqualTo(-1), $"Missing layer '{layer}'");
        }

        [TestCase("Ball", "Board", false)]
        [TestCase("Ball", "Enemy", false)]
        [TestCase("Ball", "Flipper", false)]
        [TestCase("Ball", "Trigger", false)]
        [TestCase("Enemy", "Trigger", false)]
        [TestCase("Ball", "Ball", false)]
        [TestCase("Enemy", "Enemy", true)]
        [TestCase("Enemy", "Board", true)]
        [TestCase("Enemy", "Flipper", true)]
        [TestCase("Board", "Board", true)]
        [TestCase("Board", "Flipper", true)]
        [TestCase("Flipper", "Flipper", true)]
        [TestCase("Trigger", "Board", true)]
        [TestCase("Trigger", "Trigger", true)]
        public void CollisionMatrix(string a, string b, bool ignored) =>
            Assert.That(Physics.GetIgnoreLayerCollision(LayerMask.NameToLayer(a), LayerMask.NameToLayer(b)), Is.EqualTo(ignored),
                $"{a} vs {b} should {(ignored ? "not " : "")}collide");
    }
}
