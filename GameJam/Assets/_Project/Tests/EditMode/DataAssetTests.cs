using System.Linq;
using NUnit.Framework;
using Pinball.Core;
using Pinball.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // The data assets hold exactly the values the integration/v05-multiball-side-kickers branch plays with
    // (Luck Number v0.6): its Pinball.unity scene, Settings/DefaultCardBalance.asset and
    // Settings/DefaultDifficulty.asset. "scene" marks values serialized on the old GameManager.
    public class DataAssetTests
    {
        const string Data = "Assets/_Project/Data/";

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Data + path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        static GameConfig Game => Load<GameConfig>("Config/GameConfig.asset");
        static ElementDefinition Element(string name) => Load<ElementDefinition>($"Elements/{name}.asset");

        [Test]
        public void GameConfigMatchesV06()
        {
            var g = Game;
            Assert.That(g.lives, Is.EqualTo(3));
            Assert.That(g.breachLifeCost, Is.EqualTo(1), "scene");
            Assert.That(g.launchCountdown, Is.EqualTo(3f), "scene launchWait");
            Assert.That(g.launchSpeed, Is.EqualTo(32f), "scene");
            Assert.That(g.launchSpreadDegrees, Is.EqualTo(8f));
            Assert.That(g.ballHitDamage, Is.EqualTo(4));
            Assert.That(g.empoweredHitDamage, Is.EqualTo(8));
            Assert.That(g.empoweredHitsPerCard, Is.EqualTo(1));
            Assert.That(g.diceRevealDelay, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(g.status.burnTickInterval, Is.EqualTo(0.5f));
            Assert.That(g.status.passiveStunImmunity, Is.EqualTo(1f));
            Assert.That(g.deck, Is.Not.Null);
            Assert.That(g.spawnProfile, Is.Not.Null);
            Assert.That(g.elements.Select(e => e.displayName), Is.EqualTo(new[] { "Arc", "Spark", "Shockwave" }));
        }

        [Test]
        public void MultiballMatchesV06()
        {
            var m = Game.multiball;
            Assert.That(m.maxBalls, Is.EqualTo(3), "scene");
            Assert.That(m.ballOnMultiKill, "scene");
            Assert.That(m.entryHold, Is.EqualTo(1f), "scene");
            Assert.That(m.entrySpeed, Is.EqualTo(4f), "scene");
        }

        [Test]
        public void ScoringMatchesV06()
        {
            var s = Game.scoring;
            Assert.That(s.timeInterval, Is.EqualTo(5f));
            Assert.That(s.timePoints, Is.EqualTo(5));
            Assert.That(s.comboTimeout, Is.EqualTo(3f));
            Assert.That(s.comboTiers.Select(t => (t.hits, t.points)),
                Is.EqualTo(new[] { (5, 50), (10, 100), (20, 500), (25, 750), (30, 1000), (50, 9999) }));
            Assert.That(ComboScore.Check(s.comboTiers), Is.Null);
        }

        [Test]
        public void DeckMatchesV06()
        {
            var deck = Game.deck;
            Assert.That(deck.handSize, Is.EqualTo(5));
            Assert.That(deck.startingHand, Is.Empty, "a run starts with five empty slots");
            Assert.That(deck.baseCost, Is.EqualTo(100f));
            Assert.That(deck.perCardCost, Is.EqualTo(0f));
            Assert.That(deck.naturalPerSecond, Is.EqualTo(2f));
            Assert.That(deck.perContact, Is.EqualTo(13f));
            Assert.That(deck.elementWeights.Select(w => w.element.displayName), Is.EquivalentTo(new[] { "Arc", "Spark", "Shockwave" }));
            Assert.That(deck.elementWeights.All(w => w.weight == 1f));
            Assert.That(deck.numberWeights.Select(w => w.number), Is.EqualTo(Enumerable.Range(1, 6)));
            Assert.That(deck.numberWeights.All(w => w.weight == 1f));
        }

        [Test]
        public void SpawnProfileMatchesV06()
        {
            var profile = Game.spawnProfile;
            Assert.That(profile.openingCount, Is.EqualTo(1), "scene");
            Assert.That(profile.openingColumnSpacing, Is.EqualTo(2f));
            Assert.That(profile.baseSpawnInterval, Is.EqualTo(5f));
            Assert.That(profile.spawnChance, Is.EqualTo(0.96f).Within(1e-6f));
            Assert.That(profile.placementAttempts, Is.EqualTo(30));
            Assert.That(profile.spawnSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(profile.moveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(profile.stages.Select(s => (s.time, s.spawnMultiplier, s.moveMultiplier, s.healthMultiplier)), Is.EqualTo(new[]
            {
                (0f, 1f, 1f, 1f), (30f, 1.2f, 1.25f, 2f), (90f, 1.5f, 1.3f, 4f),
                (180f, 2f, 1.5f, 6f), (300f, 5f, 1.7f, 8f), (480f, 10f, 1.8f, 10f),
            }));
            Assert.That(DifficultyCurve.Check(profile.stages), Is.Null);
            Assert.That(profile.enemies.Count, Is.EqualTo(1));
            Assert.That(profile.enemies[0].enemy.name, Is.EqualTo("Grunt"));
        }

        [Test]
        public void GruntMatchesV06()
        {
            var grunt = Load<EnemyDefinition>("Enemies/Grunt.asset");
            Assert.That(grunt.health, Is.EqualTo(9), "difficulty base enemy health");
            Assert.That(grunt.descendSeconds, Is.EqualTo(35f), "difficulty base descent time");
            Assert.That(grunt.elementRule, Is.EqualTo(EnemyDefinition.ElementRule.RandomFromGame));
            Assert.That(grunt.radius, Is.EqualTo(0.75f));
            Assert.That(grunt.hitCooldown, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(grunt.scoreValue, Is.EqualTo(50));
            Assert.That(grunt.hitBounceSpeed, Is.EqualTo(14f), "scene");
            Assert.That(grunt.queueGap, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(grunt.unstick.speed, Is.EqualTo(5f), "scene");
            Assert.That(grunt.unstick.acceleration, Is.EqualTo(20f), "scene");
        }

        [Test]
        public void ArcMatchesV06()
        {
            var arc = Element("Arc");
            Assert.That(arc.skillName, Is.EqualTo("Thunderstorm"));
            var passive = (ChainDamagePassive)arc.passive;
            Assert.That(passive.radius, Is.EqualTo(5f));
            Assert.That(passive.damage, Is.EqualTo(4));
            Assert.That(passive.damageHitEnemy, "held Arc also strikes the enemy the ball hit");
            Assert.That(Enumerable.Range(1, 6).Select(passive.nearbyTargets.Evaluate), Is.EqualTo(new[] { 3, 4, 6, 8, 13, 13 }));
            var active = (WaveDamageSkill)arc.active;
            Assert.That(active.damagePerWave, Is.EqualTo(20));
            Assert.That(active.waveInterval, Is.EqualTo(0.25f));
            AssertTiers(arc, (false, 4, 1), (false, 6, 2), (true, 4, 3), (true, 4, 4));
            Assert.That(arc.extraWavesPerCopy, Is.EqualTo(1));
        }

        [Test]
        public void SparkMatchesV06()
        {
            var spark = Element("Spark");
            Assert.That(spark.skillName, Is.EqualTo("Inferno"));
            var passive = (IgnitePassive)spark.passive;
            Assert.That(passive.igniteHitEnemy);
            Assert.That(passive.radius, Is.EqualTo(5f));
            Assert.That(passive.burnSeconds, Is.EqualTo(6f));
            Assert.That(Enumerable.Range(1, 5).Select(passive.extraTargets.Evaluate), Is.EqualTo(new[] { 2, 3, 4, 5, 6 }));
            // 0.5 DPS × multipliers 1 / 1.5 / 3 / 8 / 20.
            Assert.That(Enumerable.Range(1, 5).Select(passive.burnDps.Evaluate), Is.EqualTo(new[] { 0.5f, 0.75f, 1.5f, 4f, 10f }));
            Assert.That(((BurnSkill)spark.active).burnDps, Is.EqualTo(4f));
            AssertTiers(spark, (false, 3, 1), (false, 5, 1), (true, 4, 1), (true, 4, 1));
            Assert.That(spark.tiers.Select(t => t.burnSeconds), Is.All.EqualTo(6f));
            Assert.That(spark.extraBurnSecondsPerCopy, Is.EqualTo(2f));
        }

        [Test]
        public void ShockwaveMatchesV06()
        {
            var shock = Element("Shockwave");
            Assert.That(shock.skillName, Is.EqualTo("Impact"));
            var passive = (StunPulsePassive)shock.passive;
            Assert.That(passive.stunSeconds, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(Enumerable.Range(1, 5).Select(passive.radius.Evaluate), Is.EqualTo(new[] { 3f, 4f, 6f, 8f, 10f }));
            var active = (StunWaveSkill)shock.active;
            Assert.That(active.damagePerWave, Is.EqualTo(15));
            Assert.That(active.stunOnFirstWave);
            AssertTiers(shock, (false, 5, 1), (false, 7, 1), (true, 4, 1), (true, 4, 1));
            Assert.That(shock.tiers.Select(t => t.stunSeconds), Is.EqualTo(new[] { 5f, 10f, 15f, 20f }));
            Assert.That(shock.extraWavesPerCopy, Is.EqualTo(0), "Shockwave always resolves one impact");
        }

        [Test]
        public void ExtraCopiesAddWavesAndBurn()
        {
            Assert.That(Element("Arc").Plan(CastTier.Base, 3).waves, Is.EqualTo(3));
            Assert.That(Element("Arc").Plan(CastTier.Jackpot, 2).waves, Is.EqualTo(5));
            Assert.That(Element("Spark").Plan(CastTier.FullField, 2).burnSeconds, Is.EqualTo(8f));
            Assert.That(Element("Shockwave").Plan(CastTier.Jackpot, 5).waves, Is.EqualTo(1));
        }

        static void AssertTiers(ElementDefinition element, params (bool all, int targets, int waves)[] expected)
        {
            Assert.That(element.tiers.Length, Is.EqualTo(SkillPlanner.TierCount), element.name);
            for (int i = 0; i < expected.Length; i++)
            {
                var t = element.tiers[i];
                Assert.That((t.allTargets, t.targetCount, t.waves), Is.EqualTo(expected[i]), $"{element.name} {(CastTier)i}");
            }
        }
    }
}
