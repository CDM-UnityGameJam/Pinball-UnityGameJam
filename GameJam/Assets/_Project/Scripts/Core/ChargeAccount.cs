using System;

namespace Pinball.Core
{
    // Charge that refills the hand. Port of the v0.7 RechargeAccount:
    // - While the hand isn't full, charge builds from natural recovery and from ball contacts, uncapped.
    // - The requirement is set by how many slots are empty (DeckConfig.refillCosts). Casting more cards keeps
    //   the progress and replaces the requirement.
    // - When progress reaches the requirement every empty slot is drawn at once; progress and any overflow
    //   are cleared (FullHand).
    public sealed class ChargeAccount
    {
        bool pending;

        public double Progress { get; private set; }
        public double Required { get; private set; }

        public bool Ready => pending && Progress + 1e-8 >= Required;

        public void SetRequirement(double cost)
        {
            pending = true;
            Required = Math.Max(0, cost);
        }

        public double Earn(double amount)
        {
            if (!pending || !(amount > 0) || double.IsInfinity(amount)) return 0;
            Progress += amount;
            return amount;
        }

        public void FullHand()
        {
            Progress = 0;
            Required = 0;
            pending = false;
        }
    }
}
