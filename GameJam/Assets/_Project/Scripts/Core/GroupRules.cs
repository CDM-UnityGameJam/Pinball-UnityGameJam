using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // Whole-number groups and lucky draws (port of the integration branch's HandRules / DiceRules.ValidGroup /
    // HandOrder, "Luck Number v0.7"):
    // - A cast is a group: every held card with one number (1..MaxCards cards, each held once). Elements may mix.
    // - A group whose number is the lucky number casts Lucky skills, any other group Normal skills.
    // - Refills draw every empty slot at once. Unless the hand still holds the lucky number, each new card is
    //   a lucky candidate with probability p; one successful card is picked uniformly (duplicate numbers count
    //   once per card) and its number becomes the only lucky number. No success: no lucky number.
    // - The hand is kept sorted by number, stable within a number, empty slots last.
    public static class GroupRules
    {
        public const int MaxCards = 5;

        // 1..MaxCards distinct held cards, all with the same number, and no held card of that number left out.
        public static bool ValidGroup<T>(IList<T> cards, IList<T> hand, Func<T, int> number) where T : class
        {
            if (cards == null || hand == null || cards.Count < 1 || cards.Count > MaxCards) return false;
            int locked = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) return false;
                int n = number(card);
                if (i > 0 && n != locked) return false;
                locked = n;
                for (int j = 0; j < i; j++) if (ReferenceEquals(card, cards[j])) return false;
                bool held = false;
                foreach (var h in hand) if (ReferenceEquals(card, h)) { held = true; break; }
                if (!held) return false;
            }
            int total = 0;
            foreach (var h in hand) if (h != null && number(h) == locked) total++;
            return total == cards.Count;
        }

        // Chance that at least one of `draws` new cards becomes a lucky candidate.
        public static double LuckyChance(double probability, int draws) =>
            1 - Math.Pow(1 - Clamp01(probability), Math.Max(0, draws));

        // Reservoir sampling over the new cards' numbers: one roll per card, plus a pick roll from the second
        // success on (same call order as the old HandRules.PickLucky). 0 = no lucky number.
        public static int PickLucky(IList<int> numbers, double probability, IRandom random)
        {
            int selected = 0, successes = 0;
            foreach (int n in numbers)
            {
                double roll = random.Value();
                if (probability >= 1 || roll < probability)
                {
                    successes++;
                    if (successes == 1 || random.Value() < 1.0 / successes) selected = n;
                }
            }
            return selected;
        }

        // Index picked by weight for a roll in [0, 1]. A roll of 1 picks the last positive weight.
        public static int Weighted(IReadOnlyList<float> weights, double roll)
        {
            double total = 0;
            foreach (float w in weights) total += Math.Max(0f, w);
            double sample = roll * total;
            for (int i = 0; i < weights.Count; i++)
            {
                sample -= Math.Max(0f, weights[i]);
                if (sample < 0) return i;
            }
            for (int i = weights.Count - 1; i >= 0; i--) if (weights[i] > 0) return i;
            return 0;
        }

        // Charge needed to refill `missing` empty slots: the table's row for that count (more uses the last row).
        public static double RefillCost(IReadOnlyList<float> costs, int missing)
        {
            if (missing <= 0 || costs == null || costs.Count == 0) return 0;
            return Math.Max(0f, costs[Math.Min(missing, costs.Count) - 1]);
        }

        // Insertion sort by number: stable within a number, nulls last.
        public static void SortByNumber<T>(T[] hand, Func<T, int> number) where T : class
        {
            for (int i = 1; i < hand.Length; i++)
            {
                var item = hand[i];
                int value = item == null ? int.MaxValue : number(item);
                int j = i - 1;
                while (j >= 0 && (hand[j] == null ? int.MaxValue : number(hand[j])) > value)
                {
                    hand[j + 1] = hand[j];
                    j--;
                }
                hand[j + 1] = item;
            }
        }

        public static string Label(CastTier tier) => tier == CastTier.Lucky ? "Lucky" : "Normal";

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
