using System;
using System.Collections.Generic;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: fixed slots, playing cards, and refills (refill debt rules in Pinball.Core.RefillClock).
    // Port of the old GameManager hand code:
    // - Cards stay packed to the left: playing a card shifts the ones after it (CardShifted).
    // - Holding cards enchants the ball (CombatResolver reads HeldCount / EnabledCount).
    // - Playing a card: lock the lucky number, snapshot enemies front first, remove the card, pay the refill
    //   delay, roll Q (lucky weights on a match) and resolve it (CastScheduler).
    public sealed class HandSystem : ICombatTimer
    {
        readonly GameContext context;
        readonly CardSpec?[] slots;
        readonly RefillClock refill;

        public HandSystem(GameContext context)
        {
            this.context = context;
            var deck = context.config.deck;
            slots = new CardSpec?[Mathf.Max(1, deck.handSize)];
            refill = new RefillClock(deck.refillInterval, deck.playRefillDelay);
            for (int i = 0; i < Mathf.Min(slots.Length, deck.startingHand.Count); i++) slots[i] = deck.startingHand[i];
            if (!IsFull) refill.SetNext(context.Clock.Now + Mathf.Max(0.01f, deck.refillInterval));
        }

        public int Size => slots.Length;
        public CardSpec? this[int slot] => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        public int Count
        {
            get { int n = 0; foreach (var c in slots) if (c.HasValue) n++; return n; }
        }

        public bool IsFull => Count == Size;
        public bool Resolving { get; private set; }
        public CastResult LastPlay { get; private set; }

        public float RefillRemaining => refill.Remaining(context.Clock.Now, IsFull);
        public float RefillAfterPlay => refill.RemainingAfterPlay(context.Clock.Now, IsFull);

        // Raised before a slot's content changes, so a drag in progress can't submit a different card.
        public event Action<int> SlotReplaced;
        // (from, to) when playing a card shifts another one left.
        public event Action<int, int> CardShifted;
        // Any change to which cards are held.
        public event Action Changed;
        public event Action<CastResult> CardPlayed;

        public int HeldCount(ElementDefinition element)
        {
            int n = 0;
            foreach (var c in slots) if (c.HasValue && c.Value.element == element) n++;
            return n;
        }

        public int HeldCount(ElementDefinition element, int number)
        {
            int n = 0;
            foreach (var c in slots) if (c.HasValue && c.Value.element == element && c.Value.number == number) n++;
            return n;
        }

        // Held cards that currently enchant the ball (none while the element is short-circuited).
        public int EnabledCount(ElementDefinition element) => context.Casts.IsShortCircuited(element) ? 0 : HeldCount(element);
        public int EnabledCount(ElementDefinition element, int number) => context.Casts.IsShortCircuited(element) ? 0 : HeldCount(element, number);

        public bool TryPlay(int slot)
        {
            if (!context.Run.IsPlaying || Resolving || slot < 0 || slot >= slots.Length || !slots[slot].HasValue) return false;
            var card = slots[slot].Value;
            int lockedLucky = context.Luck.Current;
            bool wasFull = IsFull;
            // Snapshot every living enemy before the card leaves the hand and before Q is drawn.
            var snapshot = context.Enemies.FrontFirst(new List<Enemy>());
            Resolving = true;
            try
            {
                SlotReplaced?.Invoke(slot);
                slots[slot] = null;
                Compact();
                refill.OnCardPlayed(context.Clock.Now, wasFull);
                bool lucky = card.number == lockedLucky;
                int quality = context.Luck.RollQuality(lucky);
                LastPlay = context.Casts.Resolve(card, quality, lucky, lockedLucky, snapshot);
                context.Stats.RecordPlay(LastPlay);
                Changed?.Invoke();
                CardPlayed?.Invoke(LastPlay);
            }
            finally { Resolving = false; }
            return true;
        }

        // Put a card in the first empty slot (debug tools, rewards). Keeps a running refill countdown.
        public bool Grant(CardSpec card)
        {
            if (!Insert(card)) return false;
            refill.OnCardGranted(context.Clock.Now, IsFull);
            Changed?.Invoke();
            return true;
        }

        // Replace the whole hand (tests and debug tools).
        internal void SetHand(params CardSpec[] cards)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SlotReplaced?.Invoke(i);
                slots[i] = i < cards.Length ? cards[i] : (CardSpec?)null;
            }
            if (IsFull) refill.Reset();
            else refill.SetNext(context.Clock.Now + Mathf.Max(0.01f, context.config.deck.refillInterval));
            Changed?.Invoke();
        }

        public CardSpec Draw()
        {
            var deck = context.config.deck;
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
            int number = context.config.luck.minNumber;
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

        bool Insert(CardSpec card)
        {
            if (!card.element || IsFull) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].HasValue) continue;
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
                if (!slots[i].HasValue) continue;
                if (i != next)
                {
                    slots[next] = slots[i];
                    slots[i] = null;
                    CardShifted?.Invoke(i, next);
                }
                next++;
            }
        }

        public double NextEventAt => refill.NextEventAt(IsFull);

        public void OnTime(double now)
        {
            if (!refill.IsDue(now, IsFull)) return;
            Insert(Draw());
            refill.OnRefilled(now, IsFull);
            Changed?.Invoke();
        }
    }
}
