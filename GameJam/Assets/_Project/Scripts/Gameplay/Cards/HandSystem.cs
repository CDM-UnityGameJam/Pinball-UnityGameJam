using System;
using System.Collections.Generic;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: fixed slots, the lucky number, playing groups, and charge-driven refills (rules in
    // Pinball.Core.GroupRules / ChargeAccount). Port of the v0.7 GameManager hand code:
    // - A run starts with a full hand drawn for free and no lucky number.
    // - The hand is always sorted by number (stable within a number); every move raises CardShifted.
    // - Held cards enchant the ball by element, whatever their number (CombatResolver reads HeldCount). Held
    //   cards of the lucky number add each element's lucky bonus (LuckyHeldCount).
    // - Playing a group (every held card of one number): snapshot enemies front first, build one cast per
    //   element at Normal or Lucky tier, remove the cards, clear the lucky number if its last card left, set the
    //   requirement for the new number of empty slots (progress kept), then cast at once.
    // - While a card is missing, charge builds from natural recovery (every combat step) and from ball contacts
    //   (CombatResolver, after the contact's damage and enchantments). A full charge draws every empty slot at
    //   once, picks the lucky number from the new cards (unless the hand still holds it) and resets charge.
    public sealed class HandSystem
    {
        readonly GameContext context;
        readonly HandCard[] slots;
        readonly ChargeAccount charge = new ChargeAccount();
        readonly List<int> drawn = new List<int>();

        public HandSystem(GameContext context)
        {
            this.context = context;
            slots = new HandCard[Mathf.Clamp(Deck.handSize, 1, GroupRules.MaxCards)];
            DrawToFull(false);
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
        public int Missing => Size - Count;
        // True while a group or a contact is being resolved; nothing else may change the hand then.
        public bool Resolving { get; internal set; }

        // 0 = no lucky number.
        public int LuckyNumber { get; private set; }
        // Refills (and the opening draw) so far; the HUD uses it to spot new cards.
        public int DrawCount { get; private set; }

        public double ChargeProgress => charge.Progress;
        public double ChargeRequired => charge.Required;
        // The requirement after playing `count` more cards now.
        public double ChargeAfterCast(int count) => GroupRules.RefillCost(Deck.refillCosts, Missing + count);

        // Raised before a slot's content changes, so a selection can drop a card that left.
        public event Action<int> SlotReplaced;
        // (from, to) when sorting moves a card to another slot.
        public event Action<int, int> CardShifted;
        // Any change to which cards are held or to the lucky number.
        public event Action Changed;
        // (world point, amount) for charge earned from a ball contact.
        public event Action<Vector3, double> ContactCharged;
        // A group was accepted, before its skills fire.
        public event Action<CastBatch> BatchPlayed;
        // Every empty slot was drawn (not raised for the free opening hand).
        public event Action Refilled;

        public bool IsLucky(HandCard card) => card != null && LuckyNumber != 0 && card.Number == LuckyNumber;

        public int HeldCount(ElementDefinition element)
        {
            int n = 0;
            foreach (var c in slots) if (c != null && c.Element == element) n++;
            return n;
        }

        // Held cards of this element that carry the lucky number.
        public int LuckyHeldCount(ElementDefinition element)
        {
            int n = 0;
            foreach (var c in slots) if (c != null && c.Element == element && IsLucky(c)) n++;
            return n;
        }

        public int HeldCountOfNumber(int number)
        {
            int n = 0;
            foreach (var c in slots) if (c != null && c.Number == number) n++;
            return n;
        }

        public bool Contains(HandCard card) => card != null && Array.IndexOf(slots, card) >= 0;

        // Every held card with this number, in slot order.
        public List<HandCard> Group(int number, List<HandCard> into = null)
        {
            into ??= new List<HandCard>();
            into.Clear();
            foreach (var c in slots) if (c != null && c.Number == number) into.Add(c);
            return into;
        }

        public bool CanPlay => context.Run.IsPlaying && !Resolving;

        public bool IsValidGroup(IList<HandCard> cards) => GroupRules.ValidGroup(cards, slots, c => c.Number);

        // Play the group of the card in `slot`.
        public bool TryPlay(int slot)
        {
            var card = this[slot];
            return card != null && TryPlay(Group(card.Number));
        }

        // Play a group. False (and nothing changes) unless playing and the cards are every held card of one number.
        public bool TryPlay(IList<HandCard> cards)
        {
            if (!CanPlay || !IsValidGroup(cards)) return false;
            var played = new HandCard[cards.Count];
            cards.CopyTo(played, 0);
            var snapshot = context.Enemies.FrontFirst(new List<Enemy>());
            var batch = new CastBatch(played, played[0].Number == LuckyNumber, snapshot, context.config.elements);
            Resolving = true;
            try
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || Array.IndexOf(played, slots[i]) < 0) continue;
                    SlotReplaced?.Invoke(i);
                    slots[i] = null;
                }
                Sort();
                if (LuckyNumber != 0 && HeldCountOfNumber(LuckyNumber) == 0) LuckyNumber = 0;
                charge.SetRequirement(GroupRules.RefillCost(Deck.refillCosts, Missing));
                context.Stats.RecordBatch(played.Length);
                Changed?.Invoke();
                BatchPlayed?.Invoke(batch);
                context.Casts.Cast(batch);
            }
            finally { Resolving = false; }
            ResolveCharge();
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
            charge.SetRequirement(GroupRules.RefillCost(Deck.refillCosts, Missing));
            double earned = charge.Earn(amount);
            ResolveCharge();
            return earned;
        }

        void ResolveCharge()
        {
            if (!IsFull && charge.Ready) DrawToFull(true);
        }

        // Fill every empty slot. A refill (not the free opening hand) picks the lucky number from the new
        // cards unless the hand still holds the current one; the opening hand has none.
        void DrawToFull(bool refill)
        {
            bool keepLucky = refill && LuckyNumber != 0 && HeldCountOfNumber(LuckyNumber) > 0;
            drawn.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                var card = Draw();
                if (!card.element) continue;
                SlotReplaced?.Invoke(i);
                slots[i] = new HandCard(card);
                drawn.Add(card.number);
            }
            Sort();
            if (drawn.Count > 0)
            {
                if (!keepLucky) LuckyNumber = refill ? GroupRules.PickLucky(drawn, Deck.luckyProbability, context.Random) : 0;
                DrawCount++;
            }
            charge.FullHand();
            if (refill)
            {
                context.Stats.refills++;
                Refilled?.Invoke();
            }
            Changed?.Invoke();
        }

        // Put a card in an empty slot (debug tools, rewards). Keeps charge progress.
        public bool Grant(CardSpec card)
        {
            if (!card.element || IsFull) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                SlotReplaced?.Invoke(i);
                slots[i] = new HandCard(card);
                break;
            }
            Sort();
            if (IsFull) charge.FullHand();
            else charge.SetRequirement(GroupRules.RefillCost(Deck.refillCosts, Missing));
            Changed?.Invoke();
            return true;
        }

        // Debug: set the lucky number (0 = none). It needn't be held.
        public bool SetLuckyNumber(int number)
        {
            if (number < 0 || context.Run.State == RunState.GameOver) return false;
            LuckyNumber = number;
            Changed?.Invoke();
            return true;
        }

        // Debug: fill the charge so the empty slots are drawn now.
        public bool FillCharge()
        {
            if (!context.Run.IsPlaying || Resolving || IsFull) return false;
            charge.SetRequirement(GroupRules.RefillCost(Deck.refillCosts, Missing));
            charge.Earn(Math.Max(0, charge.Required - charge.Progress));
            ResolveCharge();
            return true;
        }

        // Replace the whole hand (tests and debug tools); sorted like any hand. The lucky number is unchanged.
        internal void SetHand(params CardSpec[] cards)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                SlotReplaced?.Invoke(i);
                slots[i] = i < cards.Length ? new HandCard(cards[i]) : null;
            }
            Sort();
            charge.FullHand();
            if (!IsFull) charge.SetRequirement(GroupRules.RefillCost(Deck.refillCosts, Missing));
            Changed?.Invoke();
        }

        // Number first, then element, like the old Card.Random(tuning).
        public CardSpec Draw()
        {
            var deck = Deck;
            int number = 1;
            if (deck.numberWeights.Count > 0)
            {
                var weights = new float[deck.numberWeights.Count];
                for (int i = 0; i < weights.Length; i++) weights[i] = deck.numberWeights[i].weight;
                number = deck.numberWeights[GroupRules.Weighted(weights, context.Random.Value())].number;
            }
            ElementDefinition element = null;
            if (deck.elementWeights.Count > 0)
            {
                var weights = new float[deck.elementWeights.Count];
                for (int i = 0; i < weights.Length; i++) weights[i] = deck.elementWeights[i].element ? deck.elementWeights[i].weight : 0f;
                element = deck.elementWeights[GroupRules.Weighted(weights, context.Random.Value())].element;
            }
            return new CardSpec(element, number);
        }

        void Sort()
        {
            var before = (HandCard[])slots.Clone();
            GroupRules.SortByNumber(slots, c => c.Number);
            for (int to = 0; to < slots.Length; to++)
            {
                if (slots[to] == null) continue;
                int from = Array.IndexOf(before, slots[to]);
                if (from != to) CardShifted?.Invoke(from, to);
            }
        }
    }
}
