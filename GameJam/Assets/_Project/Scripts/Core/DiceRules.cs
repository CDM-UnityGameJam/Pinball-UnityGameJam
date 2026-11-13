using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // Dice batches (port of the v0.5/v0.6 DiceRules):
    // - A batch is 1..MaxCards cards of the same number (elements may mix), each card in hand once.
    // - One fair die per card. No die showing the number: Base. One card and its die matches: Hit.
    //   Several cards and exactly one matching die: Full Field. Two or more matching dice: Jackpot.
    public static class DiceRules
    {
        public const int MaxCards = 5;
        public const int Faces = 6;

        // Chance that at least one of `count` dice shows a given face.
        public static double UpgradeChance(int count) =>
            count < 1 || count > MaxCards ? 0 : 1 - Math.Pow((Faces - 1.0) / Faces, count);

        public static bool ValidFaces(IList<int> faces, int count)
        {
            if (faces == null || faces.Count != count || count < 1 || count > MaxCards) return false;
            foreach (int face in faces) if (face < 1 || face > Faces) return false;
            return true;
        }

        public static int Matches(int number, IList<int> faces)
        {
            int count = 0;
            foreach (int face in faces) if (face == number) count++;
            return count;
        }

        public static CastTier Tier(int number, IList<int> faces)
        {
            if (faces == null || !ValidFaces(faces, faces.Count)) throw new ArgumentException("Invalid dice.");
            int matches = Matches(number, faces);
            return matches == 0 ? CastTier.Base
                : faces.Count == 1 ? CastTier.Hit
                : matches == 1 ? CastTier.FullField
                : CastTier.Jackpot;
        }

        // The tiers a batch of `count` cards can reach.
        public static CastTier[] PossibleTiers(int count) =>
            count <= 1 ? new[] { CastTier.Base, CastTier.Hit } : new[] { CastTier.Base, CastTier.FullField, CastTier.Jackpot };

        // 1..MaxCards distinct cards, all held, all with the same number.
        public static bool ValidSelection<T>(IList<T> cards, IList<T> hand, Func<T, int> number) where T : class
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
            return true;
        }

        public static int[] Roll(int count, IRandom random)
        {
            var faces = new int[count];
            for (int i = 0; i < count; i++) faces[i] = random.Range(1, Faces + 1);
            return faces;
        }

        public static string Label(CastTier tier) =>
            tier == CastTier.Base ? "Base" : tier == CastTier.Hit ? "Hit" : tier == CastTier.FullField ? "Full Field" : "Jackpot";
    }
}
