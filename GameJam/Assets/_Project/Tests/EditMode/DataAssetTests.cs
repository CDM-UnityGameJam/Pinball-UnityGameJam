using System.Linq;
using NUnit.Framework;
using Pinball.Core;
using Pinball.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // The data assets hold exactly the values the integration/v05-multiball-side-kickers branch plays with
    // (Luck Number v0.7 "lucky groups", up to 001ed3b): its Pinball.unity scene, Settings/DefaultCardBalance.asset
    // and Settings/DefaultDifficulty.asset. "scene" marks values serialized on the old GameManager.
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
        public void GameConfigMatchesV07()
        {
            var g = Game;
            Assert.That(g.lives, Is.EqualTo(3));
            Assert.That(g.breachLifeCost, Is.EqualTo(1), "scene");
            Assert.That(g.launchCountdown, Is.EqualTo(3f), "scene launchWait");
            Assert.That(g.launchSpeed, Is.EqualTo(32f), "scene");
            Assert.That(g.launchSpreadDegrees, Is.EqualTo(8f));
            Assert.That(g.ballHitDamage, Is.EqualTo(4), "collisionDamage; empowered hits are gone");
            Assert.That(g.status.burnTickInterval, Is.EqualTo(0.5f));
            Assert.That(g.status.passiveStunImmunity, Is.EqualTo(1f));
            Assert.That(g.deck, Is.Not.Null);
            Assert.That(g.spawnProfile, Is.Not.Null);
            Assert.That(g.elements.Select(e => e.displayName), Is.EqualTo(new[] { "Arc", "Spark", "Shockwave" }));
        }

        [Test]
        public void MultiballMatchesV07()
        {
            var m = Game.multiball;
            Assert.That(m.maxBalls, Is.EqualTo(3), "scene");
            Assert.That(m.ballOnMultiKill, "scene");
            Assert.That(m.entryHold, Is.EqualTo(1f), "scene");
            Assert.That(m.entrySpeed, Is.EqualTo(4f), "scene");
        }

        [Test]
        public void ScoringMatchesV07()
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
        public void DeckMatchesV07()
        {
            var deck = Game.deck;
            Assert.That(deck.handSize, Is.EqualTo(5));
            Assert.That(deck.refillCosts, Is.EqualTo(new[] { 100f, 250f, 450f, 700f, 1000f }));
            Assert.That(deck.naturalPerSecond, Is.EqualTo(3f));
            Assert.That(deck.perContact, Is.EqualTo(10f));
            Assert.That(deck.luckyProbability, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(deck.elementWeights.Select(w => w.element.displayName), Is.EqualTo(new[] { "Arc", "Spark", "Shockwave" }));
            Assert.That(deck.elementWeights.All(w => w.weight == 1f));
            Assert.That(deck.numberWeights.Select(w => w.number), Is.EqualTo(new[] { 1, 2, 3 }), "card numbers are 1-3");
            Assert.That(deck.numberWeights.All(w => w.weight == 1f));
        }

        [Test]
        public void SpawnProfileMatchesV07()
        {
            var profile = Game.spawnProfile;
            Assert.That(profile.openingCount, Is.EqualTo(1), "scene");
            Assert.That(profile.openingColumnSpacing, Is.EqualTo(2f));
            Assert.That(profile.baseSpawnInterval, Is.EqualTo(3f));
            Assert.That(profile.spawnChance, Is.EqualTo(0.96f).Within(1e-6f));
            Assert.That(profile.placementAttempts, Is.EqualTo(30));
            Assert.That(profile.spawnSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(profile.moveSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(profile.stages.Select(s => (s.time, s.spawnMultiplier, s.moveMultiplier, s.healthMultiplier)), Is.EqualTo(new[]
            {
                (0f, 1f, 1f, 1f), (30f, 1.1f, 1.25f, 2f), (90f, 1.5f, 1.3f, 4f),
                (180f, 1.8f, 1.5f, 5f), (300f, 3f, 1.7f, 7f), (480f, 5f, 1.8f, 10f),
            }));
            Assert.That(DifficultyCurve.Check(profile.stages), Is.Null);
            Assert.That(profile.enemies.Count, Is.EqualTo(1));
            Assert.That(profile.enemies[0].enemy.name, Is.EqualTo("Grunt"));
        }

        [Test]
        public void GruntMatchesV07()
        {
            var grunt = Load<EnemyDefinition>("Enemies/Grunt.asset");
            Assert.That(grunt.health, Is.EqualTo(20), "difficulty base enemy health");
            Assert.That(grunt.descendSeconds, Is.EqualTo(50f), "difficulty base descent time");
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
        public void ArcMatchesV07()
        {
            var arc = Element("Arc");
            Assert.That(arc.skillName, Is.EqualTo("Thunderstorm"));
            var passive = (ChainDamagePassive)arc.passive;
            Assert.That(passive.radius, Is.EqualTo(5f));
            Assert.That(passive.damage, Is.EqualTo(4));
            Assert.That(passive.luckyDamageBonus, Is.EqualTo(2));
            Assert.That(passive.damageHitEnemy, "held Arc also strikes the enemy the ball hit");
            Assert.That(Enumerable.Range(1, 6).Select(passive.nearbyTargets.Evaluate), Is.EqualTo(new[] { 5, 7, 10, 15, 25, 25 }));
            Assert.That(((WaveDamageSkill)arc.active).waveInterval, Is.EqualTo(0.25f));
            AssertRow(arc.normal, true, 4, waves: 2, damage: 40);
            AssertRow(arc.lucky, true, 4, waves: 3, damage: 45);
            Assert.That(arc.extraWavesPerCopy, Is.EqualTo(1));
            Assert.That(arc.extraBurnSecondsPerCopy, Is.EqualTo(0f));
        }

        [Test]
        public void SparkMatchesV07()
        {
            var spark = Element("Spark");
            Assert.That(spark.skillName, Is.EqualTo("Inferno"));
            var passive = (IgnitePassive)spark.passive;
            Assert.That(passive.igniteHitEnemy);
            Assert.That(passive.radius, Is.EqualTo(5f));
            Assert.That(passive.burnSeconds, Is.EqualTo(6f));
            Assert.That(Enumerable.Range(1, 5).Select(passive.extraTargets.Evaluate), Is.EqualTo(new[] { 5, 7, 10, 15, 25 }));
            // 0.5 DPS × multipliers 1 / 1.5 / 3 / 8 / 10.
            Assert.That(Enumerable.Range(1, 5).Select(passive.burnDps.Evaluate), Is.EqualTo(new[] { 0.5f, 0.75f, 1.5f, 4f, 5f }));
            Assert.That(passive.luckyDpsBonus, Is.EqualTo(1f));
            AssertRow(spark.normal, true, 4, waves: 1, damage: 12, burnSeconds: 6f, burnDps: 15f);
            AssertRow(spark.lucky, true, 4, waves: 1, damage: 12, burnSeconds: 15f, burnDps: 15f);
            Assert.That(spark.extraBurnSecondsPerCopy, Is.EqualTo(2f));
            Assert.That(spark.extraWavesPerCopy, Is.EqualTo(0));
        }

        [Test]
        public void ShockwaveMatchesV07()
        {
            var shock = Element("Shockwave");
            Assert.That(shock.skillName, Is.EqualTo("Impact"));
            var passive = (StunPulsePassive)shock.passive;
            Assert.That(passive.stunSeconds, Is.EqualTo(1f));
            Assert.That(passive.luckyStunBonus, Is.EqualTo(0.25f));
            Assert.That(Enumerable.Range(1, 5).Select(passive.radius.Evaluate), Is.EqualTo(new[] { 4f, 5f, 5.5f, 6f, 7f }));
            Assert.That(((StunWaveSkill)shock.active).stunOnFirstWave);
            AssertRow(shock.normal, true, 4, waves: 1, damage: 15, stunSeconds: 15f);
            AssertRow(shock.lucky, true, 4, waves: 1, damage: 15, stunSeconds: 30f);
            Assert.That(shock.extraWavesPerCopy, Is.EqualTo(0), "Shockwave always resolves one impact");
        }

        [Test]
        public void ExtraCopiesAddWavesAndBurn()
        {
            Assert.That(Element("Arc").Plan(CastTier.Normal, 3).waves, Is.EqualTo(4));
            Assert.That(Element("Arc").Plan(CastTier.Lucky, 2).waves, Is.EqualTo(4));
            Assert.That(Element("Spark").Plan(CastTier.Normal, 2).burnSeconds, Is.EqualTo(8f));
            Assert.That(Element("Spark").Plan(CastTier.Lucky, 3).burnSeconds, Is.EqualTo(19f));
            Assert.That(Element("Shockwave").Plan(CastTier.Lucky, 5).waves, Is.EqualTo(1));
        }

        static void AssertRow(ActiveTier row, bool all, int targets, int waves, int damage,
            float burnSeconds = 0f, float burnDps = 6f, float stunSeconds = 0f)
        {
            Assert.That((row.allTargets, row.targetCount, row.waves, row.damage), Is.EqualTo((all, targets, waves, damage)));
            Assert.That((row.burnSeconds, row.burnDps, row.stunSeconds), Is.EqualTo((burnSeconds, burnDps, stunSeconds)));
        }
    }
}
