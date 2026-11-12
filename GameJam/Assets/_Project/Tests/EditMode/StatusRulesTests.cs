using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    // Burn and stun scenarios ported from LuckPrototypeCheck (acceptance items 5 and the burn block),
    // driven by a real CombatClock the way the game will drive them.
    public class StatusRulesTests
    {
        sealed class Source { }

        // Minimal enemy: HP plus the two statuses, ticking burns on the clock.
        sealed class TestEnemy : ICombatTimer
        {
            public int hp;
            public readonly BurnStatus<Source> burn = new BurnStatus<Source>();
            public readonly StunStatus stun = new StunStatus();
            public TestEnemy(int hp) => this.hp = hp;

            public double NextEventAt => hp > 0 ? burn.NextTickAt : double.PositiveInfinity;

            public void OnTime(double now)
            {
                if (burn.Tick(now, LegacyV03.BurnTickInterval, LegacyV03.PassiveBurnDps, LegacyV03.ActiveBurnDps, out int damage, out _))
                    hp -= damage;
            }
        }

        CombatClock clock;

        [SetUp] public void CreateClock() => clock = new CombatClock { Running = true };

        TestEnemy Enemy(int hp)
        {
            var enemy = new TestEnemy(hp);
            clock.Register(enemy);
            return enemy;
        }

        void Ignite(TestEnemy e, bool active, float seconds) => e.burn.Ignite(clock.Now, active, seconds, LegacyV03.BurnTickInterval, null);
        bool Stun(TestEnemy e, float seconds, bool active) => e.stun.TryStun(clock.Now, seconds, active, LegacyV03.PassiveStunImmunity);

        [Test]
        public void BurnRulesMatchOldAcceptance()
        {
            var e = Enemy(100);
            Ignite(e, false, 3); clock.Advance(0.25f);
            Ignite(e, false, 3); clock.Advance(0.25f);
            Assert.That(e.hp, Is.EqualTo(98), "refresh does not reset burn tick phase");

            Ignite(e, true, 1); clock.Advance(1);
            Assert.That(e.hp, Is.EqualTo(92), "passive and active burning use max dps, not sum");

            Ignite(e, false, 3);
            Assert.That(e.burn.ActiveRemaining(clock.Now), Is.EqualTo(0).Within(0.0002), "passive cannot extend active expiry");
            clock.Advance(0.5f);
            Assert.That(e.hp, Is.EqualTo(90), "weak burn resumes after active expires");

            var odd = Enemy(100);
            Ignite(odd, true, 0.75f); clock.Advance(0.6f);
            Ignite(odd, true, 1); clock.Advance(0.4f);
            Assert.That(odd.hp, Is.EqualTo(94), "refresh between non-aligned expiry and tick preserves phase");
        }

        [Test]
        public void BurnStopsTickingWhenItExpires()
        {
            var e = Enemy(100);
            Ignite(e, false, 1);
            clock.Advance(5);
            Assert.That(e.hp, Is.EqualTo(96), "ticks at 0.5 and 1.0 only");
            Assert.That(e.burn.NextTickAt, Is.EqualTo(double.PositiveInfinity));
            Assert.That(e.burn.IsBurning(clock.Now), Is.False);
        }

        [Test]
        public void ClearedBurnNeverTicks()
        {
            var e = Enemy(100);
            Ignite(e, true, 3);
            e.burn.Clear();
            clock.Advance(3);
            Assert.That(e.hp, Is.EqualTo(100));
        }

        [Test]
        public void StunRulesMatchOldAcceptance()
        {
            var e = Enemy(100);
            Assert.That(Stun(e, 0.35f, false), "passive stun starts");
            clock.Advance(0.2f);
            Assert.That(Stun(e, 0.35f, false), Is.False, "passive cannot refresh an ongoing stun");
            clock.Advance(0.15f);
            Assert.That(e.stun.IsStunned(clock.Now), Is.False);
            Assert.That(Stun(e, 0.35f, false), Is.False, "two-second immunity begins after the stun");

            Assert.That(Stun(e, 1.2f, true), "active stun bypasses passive immunity");
            clock.Advance(0.4f);
            Stun(e, 0.3f, true);
            Assert.That(e.stun.Remaining(clock.Now), Is.EqualTo(0.8f).Within(0.0002f), "repeated active stun uses max, not addition");

            clock.Advance(0.8f);
            Assert.That(e.stun.PassiveImmunityRemaining(clock.Now), Is.EqualTo(2f).Within(0.0002f), "active end also grants passive immunity");
            clock.Advance(2);
            Assert.That(Stun(e, 0.35f, false), "passive stun works after immunity");
        }

        [Test]
        public void ZeroDurationNeverStunsOrBurns()
        {
            var e = Enemy(100);
            Assert.That(Stun(e, 0, true), Is.False);
            Ignite(e, true, 0);
            Assert.That(e.burn.IsBurning(clock.Now), Is.False);
        }

        [Test]
        public void PausedClockFreezesStatuses()
        {
            var e = Enemy(1000);
            Ignite(e, false, 3);
            Stun(e, 1, true);
            float burn = e.burn.PassiveRemaining(clock.Now), stun = e.stun.Remaining(clock.Now);

            clock.Running = false;
            clock.Advance(100);
            Assert.That(e.hp, Is.EqualTo(1000), "burn frozen");
            Assert.That(e.burn.PassiveRemaining(clock.Now), Is.EqualTo(burn), "burn duration frozen");
            Assert.That(e.stun.Remaining(clock.Now), Is.EqualTo(stun), "stun frozen");
        }
    }
}
