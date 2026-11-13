using System;

namespace Pinball.Core
{
    // Charge that refills the hand. Port of the v0.6 RechargeAccount:
    // - While the hand isn't full, charge builds from natural recovery and from ball contacts, uncapped.
    // - The first missing card costs `baseCost`. Playing a batch from a full hand restarts progress at zero;
    //   from a partial hand it keeps progress. Either way each played card adds `perCardCost` to what's needed.
    // - Every time progress reaches the requirement a card is drawn; the overflow carries over and the next
    //   card costs `baseCost` again. A full hand clears progress and requirement.
    public sealed class ChargeAccount
    {
        bool pending;

        public double Progress { get; private set; }
        public double Required { get; private set; }

        // The hand is missing a card: start charging toward `baseCost` unless already charging.
        public void StartMissing(double baseCost)
        {
            if (pending) return;
            pending = true;
            Required = Math.Max(0, baseCost);
        }

        public void PlayBatch(bool handWasFull, int count, double baseCost, double perCardCost)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (handWasFull)
            {
                Progress = 0;
                pending = true;
                Required = Math.Max(0, baseCost);
            }
            else StartMissing(baseCost);
            Required += Math.Max(0, perCardCost) * count;
        }

        // What the requirement would be after playing `count` cards now.
        public double RequiredAfterBatch(bool handFull, int count, double baseCost, double perCardCost) =>
            (handFull || !pending ? Math.Max(0, baseCost) : Required) + count * Math.Max(0, perCardCost);

        public double Earn(double amount)
        {
            if (!pending || !(amount > 0) || double.IsInfinity(amount)) return 0;
            Progress += amount;
            return amount;
        }

        // True (and pays for one card) when progress covers the requirement.
        public bool TryConsume(double baseCost)
        {
            if (!pending || Progress + 1e-8 < Required) return false;
            Progress = Math.Max(0, Progress - Required);
            Required = Math.Max(0, baseCost);
            return true;
        }

        public void FullHand()
        {
            Progress = 0;
            Required = 0;
            pending = false;
        }
    }
}
