using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    // Refill-debt scenarios ported from LuckPrototypeCheck (acceptance item 6 and the debt checks).
    public class RefillClockTests
    {
        const int HandSize = 5;

        // A hand that only counts cards, refilled by the RefillClock on the combat clock.
        sealed class TestHand : ICombatTimer
        {
            public int cards = HandSize;
            public readonly RefillClock refill = new RefillClock(LegacyV03.RefillInterval, LegacyV03.PlayRefillDelay);
            public bool Full => cards >= HandSize;

            public double NextEventAt => refill.NextEventAt(Full);

            public void OnTime(double now)
            {
                if (!refill.IsDue(now, Full)) return;
                cards++;
                refill.OnRefilled(now, Full);
            }

            public void Play(double now)
            {
                bool wasFull = Full;
                cards--;
                refill.OnCardPlayed(now, wasFull);
            }
        }

        CombatClock clock;
        TestHand hand;
        double lowEquipmentSeconds;

        [SetUp]
        public void Create()
        {
            clock = new CombatClock { Running = true };
            hand = new TestHand();
            clock.Register(hand);
            lowEquipmentSeconds = 0;
            clock.Stepped += step => { if (hand.cards <= 1) lowEquipmentSeconds += step; };
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void EveryPlayPaysAnAdditiveDelay(int count)
        {
            for (int i = 0; i < count; i++) hand.Play(clock.Now);

            double first = 8 + 4 * count;
            Assert.That(hand.refill.Remaining(clock.Now, hand.Full), Is.EqualTo(first).Within(0.0002));

            clock.Advance((float)first - 0.001f);
            Assert.That(hand.cards, Is.EqualTo(HandSize - count), "no early refill");
            clock.Advance(0.001f);
            Assert.That(hand.cards, Is.EqualTo(HandSize - count + 1), "first card at the delayed deadline");

            for (int i = 1; i < count; i++)
            {
                clock.Advance(8);
                Assert.That(hand.cards, Is.EqualTo(HandSize - count + 1 + i), "remaining cards refill every eight seconds");
            }
            Assert.That(hand.Full, "full recovery at the expected deadline");
            Assert.That(hand.refill.NextEventAt(hand.Full), Is.EqualTo(double.PositiveInfinity));

            if (count == 5) Assert.That(lowEquipmentSeconds, Is.EqualTo(36).Within(0.0002), "0-1 card time over the whole recovery");
        }

        [Test]
        public void PlayingDuringACountdownAddsTheDelayInsteadOfRestarting()
        {
            hand.Play(clock.Now);
            clock.Advance(3);
            hand.Play(clock.Now);
            Assert.That(hand.refill.Remaining(clock.Now, hand.Full), Is.EqualTo(13).Within(0.0002));
        }

        [Test]
        public void StoppedClockKeepsTheDebt()
        {
            hand.Play(clock.Now);
            float debt = hand.refill.Remaining(clock.Now, hand.Full);
            clock.Running = false; // life lost / waiting for launch
            clock.Advance(100);
            Assert.That(hand.refill.Remaining(clock.Now, hand.Full), Is.EqualTo(debt));
            Assert.That(hand.cards, Is.EqualTo(HandSize - 1));
        }

        [Test]
        public void RemainingAfterPlayPreviewsTheNextDeadline()
        {
            Assert.That(hand.refill.RemainingAfterPlay(clock.Now, true), Is.EqualTo(12f));
            hand.Play(clock.Now);
            clock.Advance(2);
            Assert.That(hand.refill.RemainingAfterPlay(clock.Now, hand.Full), Is.EqualTo(14f).Within(0.0002f));
        }

        [Test]
        public void GrantedCardsKeepARunningCountdownAndStopItWhenFull()
        {
            hand.Play(clock.Now);
            hand.Play(clock.Now);
            double deadline = hand.refill.NextAt;

            hand.cards++;
            hand.refill.OnCardGranted(clock.Now, hand.Full);
            Assert.That(hand.refill.NextAt, Is.EqualTo(deadline), "a running countdown is kept");

            hand.cards++;
            hand.refill.OnCardGranted(clock.Now, hand.Full);
            Assert.That(hand.refill.NextAt, Is.EqualTo(double.PositiveInfinity), "a full hand stops the countdown");
        }
    }
}
