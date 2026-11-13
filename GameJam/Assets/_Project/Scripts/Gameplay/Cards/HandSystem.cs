using System;
using System.Collections.Generic;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: fixed slots, playing batches, and charge-driven refills (rules in Pinball.Core.ChargeAccount).
    // Port of the v0.6 GameManager hand code:
    // - Cards stay packed to the left: playing cards shifts the ones after them (CardShifted).
    // - Held cards enchant the ball by element, whatever their number (CombatResolver reads HeldCount).
    // - Playing a batch (1..5 cards, same number): snapshot enemies front first, roll one die per card, remove
    //   the cards, add the per-card charge cost, stock empowered hits, and hand the batch to the CastScheduler,
    //   which reveals the dice after the reveal delay. Only one batch can wait for its reveal at a time.
    // - While a card is missing, charge builds from natural recovery (every combat step) and from ball contacts
    //   (CombatResolver, after the contact's damage and enchantments). Each full charge draws a card.
    public sealed class HandSystem
    {
        readonly GameContext context;
        readonly HandCard[] slots;
        readonly ChargeAccount charge = new ChargeAccount();

        public HandSystem(GameContext context)
        {
            this.context = context;
            var deck = Deck;
            slots = new HandCard[Mathf.Max(1, deck.handSize)];
            for (int i = 0; i < Mathf.Min(slots.Length, deck.startingHand.Count); i++) slots[i] = new HandCard(deck.startingHand[i]);
            if (IsFull) charge.FullHand(); else charge.StartMissing(deck.baseCost);
            context.Clock.Stepped += OnStep;
        }

        DeckConfig Deck => context.config.deck;

        public int Size => slots.Length;
        public HandCard this[int slot] => slot >= 0 && slot < slots.Length ? slots[slot] : null;
        public IReadOnlyList<HandCard> Slots => slots;

        public int Count
        {
            get { int n = 0; foreach (var c in slots) if (c != null) n++; return n; }
        }

        public bool IsFull => Count == Size;
        // True while a batch or a contact is being resolved; nothing else may change the hand then.
        public bool Resolving { get; internal set; }
        public int EmpoweredHits { get; private set; }

        public double ChargeProgress => charge.Progress;
        public double ChargeRequired => charge.Required;
        public double ChargeAfterBatch(int count) => charge.RequiredAfterBatch(IsFull, count, Deck.baseCost, Deck.perCardCost);

        // Raised before a slot's content changes, so a selection can drop a card that left.
        public event Action<int> SlotReplaced;
        // (from, to) when playing cards shifts another one left.
        public event Action<int, int> CardShifted;
        // Any change to which cards are held.
        public event Action Changed;
        // (world point, amount) for charge earned from a ball contact.
        public event Action<Vector3, double> ContactCharged;
        // A batch was accepted (its dice are not revealed yet).
        public event Action<DiceBatch> BatchPlayed;

        public int HeldCount(ElementDefinition element)
        {
            int n = 0;
            foreach (var c in slots) if (c != null && c.Element == element) n++;
            return n;
        }

        public int HeldCountOfNumber(int number)
        {
            int n = 0;
            foreach (var c in slots) if (c != null && c.Number == number) n++;
            return n;
        }

        public bool Contains(HandCard card) => card != null && Array.IndexOf(slots, card) >= 0;

        public bool CanPlay => context.Run.IsPlaying && !Resolving && context.Casts.PendingBatch == null;

        public bool IsValidBatch(IList<HandCard> cards) => DiceRules.ValidSelection(cards, slots, c => c.Number);

        // Debug: the next batch rolls these faces (it must have exactly that many cards).
        public int[] ForcedDice { get; private set; }
        public bool ForceNextDice(int[] faces)
        {
            if (faces == null || !DiceRules.ValidFaces(faces, faces.Length)) return false;
            ForcedDice = (int[])faces.Clone();
            return true;
        }

        // Play a batch. False (and nothing changes) unless playing, no batch is waiting for its reveal, and
        // the cards are 1..5 distinct held cards with the same number.
        public bool TryPlay(IList<HandCard> cards, int[] forcedDice = null)
        {
            if (!CanPlay || !IsValidBatch(cards)) return false;
            if (forcedDice == null && ForcedDice != null)
            {
                // A debug roll with the wrong count refuses the batch rather than being silently dropped.
                if (ForcedDice.Length != cards.Count) return false;
                forcedDice = ForcedDice;
                ForcedDice = null;
            }
            if (forcedDice != null && !DiceRules.ValidFaces(forcedDice, cards.Count)) return false;
            var played = new HandCard[cards.Count];
            cards.CopyTo(played, 0);
            var snapshot = context.Enemies.FrontFirst(new List<Enemy>());
            var dice = forcedDice != null ? (int[])forcedDice.Clone() : DiceRules.Roll(played.Length, context.Random);
            var batch = new DiceBatch(played, dice, context.Clock.Now + Mathf.Max(0f, context.config.diceRevealDelay), snapshot, context.config.elements);
            bool wasFull = IsFull;
            Resolving = true;
            try
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || Array.IndexOf(played, slots[i]) < 0) continue;
                    SlotReplaced?.Invoke(i);
                    slots[i] = null;
                }
                Compact();
                charge.PlayBatch(wasFull, played.Length, Deck.baseCost, Deck.perCardCost);
                EmpoweredHits = (int)Math.Min(int.MaxValue, EmpoweredHits + (long)played.Length * Math.Max(0, context.config.empoweredHitsPerCard));
                context.Stats.RecordBatch(played.Length);
                context.Casts.Accept(batch);
                Changed?.Invoke();
                BatchPlayed?.Invoke(batch);
            }
            finally { Resolving = false; }
            return true;
        }

        // Uses one empowered hit if any. Returns true if one was used.
        internal bool ConsumeEmpoweredHit()
        {
            if (EmpoweredHits <= 0) return false;
            EmpoweredHits--;
            context.Stats.empoweredUsed++;
            return true;
        }

        internal double EarnContactCharge(Vector3 point)
        {
            double earned = Earn(Deck.perContact);
            if (earned > 0) ContactCharged?.Invoke(point, earned);
            return earned;
        }

        void OnStep(double step)
        {
            context.Stats.Tick(step, Count);
            Earn(Deck.naturalPerSecond * step);
        }

        double Earn(double amount)
        {
            if (!context.Run.IsPlaying || IsFull) return 0;
            charge.StartMissing(Deck.baseCost);
            double earned = charge.Earn(amount);
            DrawWhileCharged();
            return earned;
        }

        void DrawWhileCharged()
        {
            bool drew = false;
            while (!IsFull && charge.TryConsume(Deck.baseCost))
            {
                Insert(new HandCard(Draw()));
                drew = true;
            }
            if (IsFull) charge.FullHand();
            if (drew) Changed?.Invoke();
        }

        // Put a card in the first empty slot (debug tools, rewards). Keeps charge progress.
        public bool Grant(CardSpec card)
        {
            if (!card.element || !Insert(new HandCard(card))) return false;
            if (IsFull) charge.FullHand();
            Changed?.Invoke();
            return true;
        }

        // Debug: fill the charge so the next missing card arrives now.
        public bool FillCharge()
        {
            if (!context.Run.IsPlaying || Resolving || IsFull) return false;
            charge.StartMissing(Deck.baseCost);
            charge.Earn(Math.Max(0, charge.Required - charge.Progress));
            DrawWhileCharged();
            return true;
        }

        // Replace the whole hand (tests and debug tools).
        internal void SetHand(params CardSpec[] cards)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SlotReplaced?.Invoke(i);
                slots[i] = i < cards.Length ? new HandCard(cards[i]) : null;
            }
            charge.FullHand();
            if (!IsFull) charge.StartMissing(Deck.baseCost);
            Changed?.Invoke();
        }

        internal void ClearEmpoweredHits() => EmpoweredHits = 0;

        public CardSpec Draw()
        {
            var deck = Deck;
            ElementDefinition element = null;
            float total = 0f;
            foreach (var w in deck.elementWeights) if (w.element) total += Mathf.Max(0f, w.weight);
            float pick = context.Random.Value() * total;
            foreach (var w in deck.elementWeights)
            {
                if (!w.element || w.weight <= 0f) continue;
                element = w.element;
                if (pick < w.weight) break;
                pick -= w.weight;
            }
            int number = 1;
            total = 0f;
            foreach (var w in deck.numberWeights) total += Mathf.Max(0f, w.weight);
            pick = context.Random.Value() * total;
            foreach (var w in deck.numberWeights)
            {
                if (w.weight <= 0f) continue;
                number = w.number;
                if (pick < w.weight) break;
                pick -= w.weight;
            }
            return new CardSpec(element, number);
        }

        bool Insert(HandCard card)
        {
            if (card == null || !card.Element || IsFull) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                SlotReplaced?.Invoke(i);
                slots[i] = card;
                return true;
            }
            return false;
        }

        void Compact()
        {
            int next = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (i != next)
                {
                    slots[next] = slots[i];
                    slots[i] = null;
                    CardShifted?.Invoke(i, next);
                }
                next++;
            }
        }
    }
}
