using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Core;
using Pinball.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // The data assets hold exactly the values the old Pinball.unity plays with (Luck Number v0.3).
    // Scene values that differ from the old code defaults are marked "scene".
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
        public void GameConfigMatchesV03()
        {
            var g = Game;
            Assert.That(g.lives, Is.EqualTo(3));
            Assert.That(g.drainLifeCost, Is.EqualTo(1));
            Assert.That(g.breachLifeCost, Is.EqualTo(1));
            Assert.That(g.launchCountdown, Is.EqualTo(3f));
            Assert.That(g.launchSpeed, Is.EqualTo(32f), "scene");
            Assert.That(g.launchSpreadDegrees, Is.EqualTo(8f));
            Assert.That(g.ballHitDamage, Is.EqualTo(4));
            Assert.That(g.status.burnTickInterval, Is.EqualTo(LegacyV03.BurnTickInterval));
            Assert.That(g.status.passiveStunImmunity, Is.EqualTo(LegacyV03.PassiveStunImmunity));
            Assert.That(g.luck, Is.Not.Null);
            Assert.That(g.deck, Is.Not.Null);
            Assert.That(g.spawnProfile, Is.Not.Null);
            Assert.That(g.elements.Select(e => e.displayName), Is.EqualTo(new[] { "Arc", "Spark", "Shockwave" }));
        }

        [Test]
        public void LuckConfigMatchesV03()
        {
            var luck = Game.luck;
            Assert.That(luck.minNumber, Is.EqualTo(LegacyV03.MinNumber));
            Assert.That(luck.maxNumber, Is.EqualTo(LegacyV03.MaxNumber));
            Assert.That(luck.luckyRefreshInterval, Is.EqualTo(10f));
            Assert.That(luck.shortCircuitMinSeconds, Is.EqualTo(LegacyV03.ShortCircuitMin));
            Assert.That(luck.shortCircuitMaxSeconds, Is.EqualTo(LegacyV03.ShortCircuitMax));
            var expected = LegacyV03.Bands();
            Assert.That(luck.bands.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                var a = luck.bands[i];
                var e = expected[i];
                Assert.That((a.id, a.minQ, a.maxQ, a.normalWeight, a.luckyWeight), Is.EqualTo((e.id, e.minQ, e.maxQ, e.normalWeight, e.luckyWeight)));
            }
            Assert.That(LuckConfigValidation.Check(luck.bands), Is.Null);
        }

        // End to end: the element assets plus the luck asset reproduce every old skill plan.
        [Test]
        public void ElementAssetsReproduceEveryOldSkillPlan()
        {
            var luck = Game.luck;
            foreach (var row in Golden.Load().plans)
            {
                var plan = Element(row.element).Plan(luck, row.q);
                string at = $"{row.element} Q{row.q}";
                Assert.That(plan.allTargets, Is.EqualTo(row.all), at);
                Assert.That(plan.targetCount, Is.EqualTo(row.targets), at);
                Assert.That(plan.waves, Is.EqualTo(row.waves), at);
                Assert.That(plan.burnSeconds, Is.EqualTo(row.burn).Within(1e-5f), at);
                Assert.That(plan.stunSeconds, Is.EqualTo(row.stun).Within(1e-5f), at);
            }
        }

        [Test]
        public void EveryElementScalesEveryCastingBand()
        {
            var g = Game;
            var castingBands = g.luck.bands.Where(b => b.maxQ > 0).Select(b => b.id).ToList();
            foreach (var element in g.elements)
            {
                Assert.That(element.passive, Is.Not.Null, element.name + " passive");
                Assert.That(element.active, Is.Not.Null, element.name + " active");
                var scaled = element.bandScaling.Select(s => s.bandId).ToList();
                Assert.That(scaled, Is.EquivalentTo(castingBands), element.name + " band scaling ids");
            }
        }

        [Test]
        public void ArcModulesMatchV03()
        {
            var arc = Element("Arc");
            Assert.That(arc.skillName, Is.EqualTo("Thunderstorm"));
            var passive = (ChainDamagePassive)arc.passive;
            Assert.That(passive.radius, Is.EqualTo(3f));
            Assert.That(passive.damage, Is.EqualTo(4));
            var active = (WaveDamageSkill)arc.active;
            Assert.That(active.damagePerWave, Is.EqualTo(12));
            Assert.That(active.waveInterval, Is.EqualTo(0.25f));
            for (int cards = 0; cards <= 6; cards++)
                Assert.That(passive.maxTargets.Evaluate(cards), Is.EqualTo(Golden.Load().arcTargets[cards]));
        }

        [Test]
        public void SparkModulesMatchV03()
        {
            var spark = Element("Spark");
            Assert.That(spark.skillName, Is.EqualTo("Inferno"));
            var passive = (IgnitePassive)spark.passive;
            Assert.That(passive.igniteHitEnemy);
            Assert.That(passive.radius, Is.EqualTo(2.5f));
            Assert.That(passive.burnSeconds, Is.EqualTo(3f));
            Assert.That(passive.burnDps, Is.EqualTo(LegacyV03.PassiveBurnDps));
            Assert.That(((BurnSkill)spark.active).burnDps, Is.EqualTo(LegacyV03.ActiveBurnDps));
            for (int cards = 0; cards <= 6; cards++)
                Assert.That(passive.extraTargets.Evaluate(cards), Is.EqualTo(Golden.Load().extraSparkTargets[cards]));
        }

        [Test]
        public void ShockwaveModulesMatchV03()
        {
            var shock = Element("Shockwave");
            Assert.That(shock.skillName, Is.EqualTo("Impact"));
            var passive = (StunPulsePassive)shock.passive;
            Assert.That(passive.stunSeconds, Is.EqualTo(0.35f));
            var active = (StunWaveSkill)shock.active;
            Assert.That(active.damagePerWave, Is.EqualTo(8));
            Assert.That(active.waveInterval, Is.EqualTo(0.35f));
            Assert.That(active.stunOnFirstWave);
            for (int cards = 0; cards <= 6; cards++)
                Assert.That(passive.radius.Evaluate(cards), Is.EqualTo(Golden.Load().shockRadius[cards]).Within(1e-5f));
        }

        [Test]
        public void DeckMatchesV03()
        {
            var deck = Game.deck;
            Assert.That(deck.handSize, Is.EqualTo(5));
            Assert.That(deck.refillInterval, Is.EqualTo(LegacyV03.RefillInterval));
            Assert.That(deck.playRefillDelay, Is.EqualTo(LegacyV03.PlayRefillDelay));
            Assert.That(deck.startingHand.Select(c => $"{c.number} {c.element.displayName}"),
                Is.EqualTo(new[] { "6 Spark", "6 Shockwave", "2 Arc", "4 Arc", "5 Spark" }));
            Assert.That(deck.elementWeights.Select(w => w.element.displayName), Is.EquivalentTo(new[] { "Arc", "Spark", "Shockwave" }));
            Assert.That(deck.elementWeights.All(w => w.weight == 1f));
            Assert.That(deck.numberWeights.Select(w => w.number), Is.EqualTo(Enumerable.Range(1, 6)));
            Assert.That(deck.numberWeights.All(w => w.weight == 1f));
        }

        [Test]
        public void SpawnProfileMatchesV03()
        {
            var profile = Game.spawnProfile;
            Assert.That(profile.spawnInterval, Is.EqualTo(2f), "scene");
            Assert.That(profile.spawnChance, Is.EqualTo(0.96f).Within(1e-6f));
            Assert.That(profile.openingCount, Is.EqualTo(3));
            Assert.That(profile.openingColumnSpacing, Is.EqualTo(2f));
            Assert.That(profile.placementAttempts, Is.EqualTo(30));
            Assert.That(profile.enemies.Count, Is.EqualTo(1));
            Assert.That(profile.enemies[0].enemy.name, Is.EqualTo("Grunt"));
            Assert.That(profile.enemies[0].weight, Is.EqualTo(1f));
            foreach (float minutes in new[] { 0f, 3f, 10f, 30f })
            {
                Assert.That(profile.spawnIntervalOverTime.Evaluate(minutes), Is.EqualTo(1f));
                Assert.That(profile.marchSpeedOverTime.Evaluate(minutes), Is.EqualTo(1f));
            }
        }

        [Test]
        public void GruntMatchesV03()
        {
            var grunt = Load<EnemyDefinition>("Enemies/Grunt.asset");
            Assert.That(grunt.health, Is.EqualTo(40));
            Assert.That(grunt.radius, Is.EqualTo(0.75f));
            Assert.That(grunt.hitCooldown, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(grunt.scoreValue, Is.EqualTo(50));
            Assert.That(grunt.numberRule, Is.EqualTo(EnemyDefinition.NumberRule.RandomInLuckRange));
            Assert.That(grunt.descendSeconds, Is.EqualTo(40f));
            Assert.That(grunt.queueGap, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(grunt.horizontalGap, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(grunt.verticalGap, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(grunt.ballClearance, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(grunt.wallClearance, Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(grunt.unstick.speed, Is.EqualTo(5f), "scene");
            Assert.That(grunt.unstick.acceleration, Is.EqualTo(20f), "scene");
            Assert.That(grunt.unstick.forwardBias, Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(grunt.unstick.sideBias, Is.EqualTo(0.15f).Within(1e-6f));
        }

        static List<QualityBand> Bands() => LegacyV03.Bands();

        [Test]
        public void ValidationRejectsBadBandTables()
        {
            var overlap = Bands(); overlap[2].minQ = 15;
            Assert.That(LuckConfigValidation.Check(overlap), Does.Contain("overlap"));

            var duplicate = Bands(); duplicate[3].id = "Low";
            Assert.That(LuckConfigValidation.Check(duplicate), Does.Contain("Two bands"));

            var noNormal = Bands(); noNormal.ForEach(b => b.normalWeight = 0);
            Assert.That(LuckConfigValidation.Check(noNormal), Does.Contain("normal weight"));

            var noZero = Bands(); noZero.RemoveAt(1);
            Assert.That(LuckConfigValidation.Check(noZero), Does.Contain("Q = 0"));

            var inverted = Bands(); inverted[0].minQ = 5;
            Assert.That(LuckConfigValidation.Check(inverted), Does.Contain("min Q"));

            Assert.That(LuckConfigValidation.Check(new List<QualityBand>()), Is.Not.Null);
        }
    }
}
