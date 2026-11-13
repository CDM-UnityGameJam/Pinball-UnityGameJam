using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The hand: its size, the cards a run starts with, how replacements are drawn and what they cost.
    [CreateAssetMenu(fileName = "DeckConfig", menuName = "Pinball/Config/Deck Config", order = 2)]
    public sealed class DeckConfig : ScriptableObject
    {
        [Title("Hand")]
        [MinValue(1)] public int handSize = 5;
        [ValidateInput(nameof(FitsHand), "The starting hand has more cards than the hand size.")]
        public List<CardSpec> startingHand = new List<CardSpec>();
        [MinValue(1), Tooltip("Card numbers run from 1 to this. Each played card rolls one die with this many faces.")]
        public int maxNumber = 6;

        [Title("Charge (refill)")]
        [InfoBox("While the hand isn't full, charge builds up; each time it reaches the requirement a card is drawn. " +
                 "The first missing card needs Base Cost; each card played adds Per Card Cost on top.")]
        [MinValue(0)] public float baseCost = 100f;
        [MinValue(0)] public float perCardCost = 0f;
        [MinValue(0), SuffixLabel("/s"), Tooltip("Charge per combat second while a card is missing.")]
        public float naturalPerSecond = 2f;
        [MinValue(0), Tooltip("Charge for every new ball contact with an enemy (even inside its damage cooldown).")]
        public float perContact = 13f;

        [Title("Draw table")]
        [InfoBox("Refills pick an element and a number independently, by weight.")]
        public List<ElementWeight> elementWeights = new List<ElementWeight>();
        public List<NumberWeight> numberWeights = new List<NumberWeight>();

        bool FitsHand(List<CardSpec> value) => value == null || value.Count <= handSize;
    }

    [Serializable]
    public struct CardSpec
    {
        [HorizontalGroup, HideLabel, Required] public ElementDefinition element;
        [HorizontalGroup(60), LabelWidth(20), LabelText("#")] public int number;

        public CardSpec(ElementDefinition element, int number) { this.element = element; this.number = number; }

        public override string ToString() => $"{number} · {(element ? element.displayName : "?")}";
    }

    [Serializable]
    public struct ElementWeight
    {
        [HorizontalGroup, HideLabel, Required] public ElementDefinition element;
        [HorizontalGroup(90), MinValue(0), LabelWidth(45)] public float weight;
    }

    [Serializable]
    public struct NumberWeight
    {
        [HorizontalGroup, LabelWidth(50)] public int number;
        [HorizontalGroup, MinValue(0), LabelWidth(45)] public float weight;
    }
}
